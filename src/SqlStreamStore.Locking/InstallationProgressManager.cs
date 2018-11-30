using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public class InstallationProgressManager
    {
        private readonly IInstallRepository _installRepo;
        private readonly DelayBy _delayBy;

        private TimeSpan _timeout = TimeSpan.FromSeconds(30);

        public InstallationProgressManager(IInstallRepository installRepo, DelayBy delayBy = null)
        {
            _installRepo = installRepo;
            _delayBy = delayBy ?? Task.Delay;
        }

        public const string NotStarted = "NotStarted";
        public async Task<ISingleProcessLock> AquireSingleProcessLock(CancellationToken ct)
        {
            // Query for existing lock
            var history = await _installRepo.Get(ct);

            if (history.Version == -1)
            {
                return new SingleProcessSingleProcessLock(this, history, CancellationTokenSource.CreateLinkedTokenSource(ct));
            }

            int currentVersion = history.Version;
            while (!ct.IsCancellationRequested)
            {
                await _delayBy(_timeout, ct);
                history = await _installRepo.Get(ct);

                if (history.Version == currentVersion)
                {
                    // it appears no new data has arrived 
                    var transferredOwnershipStep = LockData.CompletedStep.FromEnvironment(currentVersion + 1, history.LastCompletedStep, "Transferred ownership of task");
                    await _installRepo.Remember(history, ct, transferredOwnershipStep);
                    return new SingleProcessSingleProcessLock(this, history,
                        CancellationTokenSource.CreateLinkedTokenSource(ct));
                }
                else
                {
                    currentVersion = history.Version;
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
            await _installRepo.Remember(
                currentHistory: singleProcessLock.CurrentHistory,
                ct: ct,
                newlyCompletedStep: newStep);

            await singleProcessLock.ReportAliveAsync(ct);
            return step;
        }
    }
}