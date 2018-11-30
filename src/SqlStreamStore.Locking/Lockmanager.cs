using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public class Lockmanager
    {
        internal readonly SchedulePeriodicAction SchedulePeriodicAction;
        internal readonly DelayBy DelayBy;
        private readonly ILockStore _lockStore;

        private TimeSpan _timeout = TimeSpan.FromSeconds(30);

        public Lockmanager(ILockStore lockStore, DelayBy delayBy = null, SchedulePeriodicAction schedulePeriodicAction = null)
        {
            SchedulePeriodicAction = schedulePeriodicAction;
            _lockStore = lockStore;
            DelayBy = delayBy ?? Task.Delay;
            SchedulePeriodicAction = schedulePeriodicAction;
        }

        public async Task<ISingleProcessLock> AquireSingleProcessLock(CancellationToken ct)
        {
            // Query for existing lock
            var lockData = await _lockStore.Get(ct);

            if (lockData.CanAquireLock)
            {
                var acquiredLock = lockData.Acquired();
                await _lockStore.Store(acquiredLock, ct);
                return new SingleProcessSingleProcessLock(_lockStore, acquiredLock, DelayBy, ct);
            }

            int currentVersion = lockData.Version;
            while (!ct.IsCancellationRequested)
            {
                await DelayBy(_timeout, ct);
                lockData = await _lockStore.Get(ct);

                if (lockData.Version == currentVersion || lockData.CanAquireLock)
                {
                    // No progress, so we can safely take over
                    var acquiredLock = lockData.TakeOver();
                    await _lockStore.Store(acquiredLock, ct);
                    return new SingleProcessSingleProcessLock(_lockStore, acquiredLock, DelayBy, ct);
                }
                else
                {
                    currentVersion = lockData.Version;
                }
            }

            throw new InvalidOperationException("shouldn't be reached");
            //// If found.. keep polling.. until new row or timeout
            //// if not found, insert row and keep polling


        }
        public async Task<string> GetLastCompletedInstallationStep(CancellationToken ct)
        {
            return NotStarted;
        }
        public async Task<string> RememberLastCompletedInstallationStep(string step, ISingleProcessLock singleProcessLock, CancellationToken ct)
        {
            var newStep = LockData.CompletedStep.FromEnvironment(
                singleProcessLock.CurrentHistory.Version + 1, step, "");
            await _lockStore.Store(
                currentHistory: singleProcessLock.CurrentHistory,
                ct: ct,
                newlyCompletedStep: newStep);

            await singleProcessLock.ReportAliveAsync(ct);
            return step;
        }
    }
}