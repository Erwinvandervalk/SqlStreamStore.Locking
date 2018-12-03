using System;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore.Locking.Data;

namespace SqlStreamStore.Locking
{
    public class SingleProcessSingleProcessLock : ISingleProcessLock
    {
        private readonly ILockStore _lockStore;
        internal readonly ScheduleRecurring ScheduleRecurring;
        private readonly LockManager.Options _options;
        private readonly CancellationTokenSource _installationCancelledCts;
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);
        private Task<Task> _tickTask;

        private TimeSpan _elapsed;
        private readonly Func<Task> _stopPolling;

        public SingleProcessSingleProcessLock(ILockStore lockStore, LockData lockData, ScheduleRecurring scheduleRecurring, LockManager.Options options, CancellationToken ct)
        {
            _lockStore = lockStore;
            ScheduleRecurring = scheduleRecurring;
            _options = options;
            _installationCancelledCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _elapsed = TimeSpan.Zero;
            
            CurrentLockData = lockData;
            _stopPolling = ScheduleRecurring(Tick, OnTick, ct);
        }

        private async Task OnTick(CancellationToken ct)
        {
            _elapsed = _elapsed.Add(Tick);

            if (_elapsed > _options.TaskTimeout)
            {
                await TimeoutOccurred(ct);
            }
            else
            {
                var cancelledLock = CurrentLockData.Renewed();
                await StoreLockData(cancelledLock, ct);
            }
        }

        private async Task TimeoutOccurred(CancellationToken ct)
        {
            await _stopPolling();

            // Trigger the cancelled CTS, which should cancel any running task. 
            _installationCancelledCts.Cancel();

            // Store the cancellation in the db
            var cancelledLock = CurrentLockData.AfterAction(LockAction.Cancelled);
            await StoreLockData(cancelledLock, ct);
        }

        private async Task StoreLockData(LockData cancelledLock, CancellationToken ct)
        {
            await _lockStore.Save(cancelledLock, ct);
            CurrentLockData = cancelledLock;
        }

        public async Task ReportAlive(string state, CancellationToken ct, bool clearHistory = false)
        {
            ct.ThrowIfCancellationRequested();
            var renewedLock = string.IsNullOrEmpty(state)
                ? CurrentLockData.Renewed(clearHistory)
                : CurrentLockData.WithProgress(state, clearHistory);

            await StoreLockData(renewedLock, ct);
            _elapsed = TimeSpan.Zero;
        }
        public async Task Release(CancellationToken ct, bool clearHistory = false)
        {
            await _stopPolling();
            await _lockStore.Save(CurrentLockData.AfterAction(LockAction.Released, clearHistory), ct);
            _installationCancelledCts.Cancel();
        }
        public CancellationToken InstallCancelled => _installationCancelledCts.Token;

        public LockData CurrentLockData { get; private set; }

        public void Dispose()
        {
            _stopPolling?.Invoke().GetAwaiter().GetResult();
            _installationCancelledCts.Dispose();
        }
    }
}