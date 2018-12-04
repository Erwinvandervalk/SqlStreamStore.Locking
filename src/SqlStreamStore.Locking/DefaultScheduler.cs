using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    internal static class DefaultScheduler
    {
        public static Func<Task> ScheduleRecurring(TimeSpan period, Func<CancellationToken, Task> taskToSchedule,
            CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var task = Task.Run(async () =>
            {
                await Task.Delay(period, cts.Token);
                await taskToSchedule(cts.Token);
            }, cts.Token);

            return async () =>
            {
                cts.Cancel();
                await task;
            };
        }
    }
}