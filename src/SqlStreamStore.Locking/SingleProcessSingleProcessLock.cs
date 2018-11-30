using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public class SingleProcessSingleProcessLock : ISingleProcessLock
    {
        private readonly ILockStore _lockStore;
        internal readonly DelayBy DelayBy;
        private readonly CancellationTokenSource _installationCancelledCts;
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan TimeoutAfter = TimeSpan.FromSeconds(20);
        private CancellationTokenSource _stopped;
        private Task<Task> _tickTask;

        private TimeSpan _elapsed;

        public SingleProcessSingleProcessLock(ILockStore lockStore, LockData lockData, DelayBy delayBy, CancellationToken ct)
        {
            _lockStore = lockStore;
            DelayBy = delayBy;
            _installationCancelledCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _stopped = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _elapsed = TimeSpan.Zero;
            _tickTask = Task.Factory.StartNew(TickLoop, _stopped.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            CurrentLockData = lockData;
        }

        private async Task TickLoop()
        {
            while (!_stopped.IsCancellationRequested)
            {
                var ct = _stopped.Token;
                await Task.Delay(Tick, ct);
                _elapsed = _elapsed.Add(Tick);

                if (_elapsed > TimeoutAfter)
                {
                    await TimeoutOccurred(ct);
                    _stopped.Cancel();
                }
                else
                {
                    var cancelledLock = CurrentLockData.Renewed();
                    await StoreLockData(cancelledLock, ct);
                }
            }
        }

        private async Task TimeoutOccurred(CancellationToken ct)
        {
            // Trigger the cancelled CTS, which should cancel any running task. 
            _installationCancelledCts.Cancel();

            // Store the cancellation in the db
            var cancelledLock = CurrentLockData.AfterAction(LockAction.Cancelled);
            await StoreLockData(cancelledLock, ct);
        }

        private async Task StoreLockData(LockData cancelledLock, CancellationToken ct)
        {
            await _lockStore.Store(cancelledLock, ct);
            CurrentLockData = cancelledLock;
        }

        public async Task ReportAliveAsync(string state, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var renewedLock = string.IsNullOrEmpty(state)
                ? CurrentLockData.Renewed()
                : CurrentLockData.WithProgress(state);

            await StoreLockData(renewedLock, ct);
        }
        public Task Release(CancellationToken ct)
        {
            _lockStore.Store(CurrentLockData.AfterAction(LockAction.Released), ct);
            return Task.CompletedTask;
        }
        public CancellationToken InstallCancelled => _installationCancelledCts.Token;
        public LockData CurrentLockData { get; private set; }

        public void Dispose()
        {
            _installationCancelledCts.Dispose();
        }
    }
}