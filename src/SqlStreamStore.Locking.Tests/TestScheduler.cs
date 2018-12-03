using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking.Tests
{
    public class TestScheduler
    {
        private readonly List<ScheduledTask> _scheduledTasks = new List<ScheduledTask>();
        private readonly object _syncRoot = new object();

        public Func<Task> ScheduleRecurring(TimeSpan period, Func<CancellationToken, Task> tasktoschedule,
            CancellationToken ct)
        {
            var scheduledTask = new ScheduledTask(tasktoschedule, period);
            lock (_syncRoot)
            {
                _scheduledTasks.Add(scheduledTask);
            }

            return () =>
            {
                lock (_syncRoot)
                {
                    _scheduledTasks.Remove(scheduledTask);
                }

                return Task.CompletedTask;
            };
        }

        public async Task AdvanceTimeBy(TimeSpan toAdvance)
        {
            ScheduledTask[] scheduledTasks;

            lock (_syncRoot)
            {
                scheduledTasks = _scheduledTasks.ToArray();
            }

            foreach (var task in scheduledTasks) task.ElapsedSinceLastTrigger += toAdvance;


            while (true)
            {
                var ticked = false;

                foreach (var task in scheduledTasks)
                    if (task.ElapsedSinceLastTrigger >= task.Period)
                    {
                        ticked = true;
                        task.ElapsedSinceLastTrigger -= task.Period;
                        await task.ToSchedule(CancellationToken.None);
                    }

                if (!ticked) break;
            }
        }

        private class ScheduledTask
        {
            public readonly TimeSpan Period;
            public readonly Func<CancellationToken, Task> ToSchedule;
            public TimeSpan ElapsedSinceLastTrigger;

            public ScheduledTask(Func<CancellationToken, Task> schedule, TimeSpan period)
            {
                ToSchedule = schedule;
                Period = period;
            }
        }
    }
}