using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public class LockManager
    {
        internal readonly DelayBy DelayBy;
        private readonly ILockStore _lockStore;

        private TimeSpan _timeout = TimeSpan.FromSeconds(60);

        public LockManager(ILockStore lockStore, DelayBy delayBy = null)
        {
            _lockStore = lockStore;
            DelayBy = delayBy ?? Task.Delay;
        }

        public async Task<ISingleProcessLock> AquireSingleProcessLock(CancellationToken ct)
        {
            // Query for existing lock
            var lockData = await _lockStore.Get(ct);

            if (lockData.CanAquireLock)
            {
                var acquiredLock = lockData.Acquired();
                await _lockStore.Save(acquiredLock, ct);
                return new SingleProcessSingleProcessLock(_lockStore, acquiredLock, DelayBy, ct);
            }

            var elapsed = TimeSpan.Zero;

            int currentVersion = lockData.Version;
            while (!ct.IsCancellationRequested)
            {
                elapsed += TimeSpan.FromSeconds(1);
                await DelayBy(TimeSpan.FromSeconds(1), ct);
                lockData = await _lockStore.Get(ct);

                if (lockData.CanAquireLock || (lockData.Version == currentVersion && elapsed > _timeout))
                {
                    // No progress, so we can safely take over
                    var acquiredLock = lockData.TakeOver();
                    await _lockStore.Save(acquiredLock, ct);
                    return new SingleProcessSingleProcessLock(_lockStore, acquiredLock, DelayBy, ct);
                }
                else
                {
                    currentVersion = lockData.Version;
                }
            }
            throw new InvalidOperationException("shouldn't be reached");
        }
        public async Task<string> GetLastCompletedInstallationStep(CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public async Task<string> RememberLastCompletedInstallationStep(string step, ISingleProcessLock singleProcessLock, CancellationToken ct)
        {
            throw new NotImplementedException();
            //var newStep = LockData.CompletedStep.FromEnvironment(
            //    singleProcessLock.CurrentHistory.Version + 1, step, "");
            //await _lockStore.Store(
            //    currentHistory: singleProcessLock.CurrentHistory,
            //    ct: ct,
            //    newlyCompletedStep: newStep);

            //await singleProcessLock.ReportAliveAsync(ct);
            //return step;
        }
    }
}