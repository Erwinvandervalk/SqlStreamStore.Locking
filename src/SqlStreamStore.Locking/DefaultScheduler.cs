using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    internal static class DefaultScheduler
    {
        public static Func<Task> ScheduleRecurring(TimeSpan period, Func<CancellationToken, Task> tasktoschedule, CancellationToken ct)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var task = Task.Run(async () =>
            {
                await Task.Delay(period, cts.Token);
                await tasktoschedule(cts.Token);
            }, cts.Token);

            return async () =>
            {
                cts.Cancel();
                await task;
            };
        }
    }
}