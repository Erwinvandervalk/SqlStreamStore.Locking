using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public class SingleProcessSingleProcessLock : ISingleProcessLock
    {
        private readonly InstallationProgressManager _mgr;
        private readonly CancellationTokenSource _cts;
        private readonly ScheduleTask ScheduleTask;
        private static readonly TimeSpan s_TimeSpan = TimeSpan.FromSeconds(10);
        private IDisposable _scheduledTask;

        public SingleProcessSingleProcessLock(InstallationProgressManager mgr, LockData history, CancellationTokenSource cts, ScheduleTask scheduleTask)
        {
            _mgr = mgr;
            _cts = cts;
            ScheduleTask = scheduleTask;
            _scheduledTask = ScheduleTask(s_TimeSpan, OnTime);
            CurrentHistory = history;
        }
        private async Task OnTime(CancellationToken ct)
        {
            _cts.Cancel();
        }
        public Task ReportAliveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scheduledTask?.Dispose();
            _scheduledTask = ScheduleTask(s_TimeSpan, OnTime);
            return Task.CompletedTask;
        }
        public Task Release(CancellationToken ct)
        {
            _scheduledTask?.Dispose();
            return Task.CompletedTask;
        }
        public CancellationToken InstallCancelled => _cts.Token;
        public LockData CurrentHistory { get; }

        public void Dispose()
        {
            _cts.Dispose();
            _scheduledTask?.Dispose();
        }
    }
}