using System;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore.Locking.Data;

namespace SqlStreamStore.Locking
{
    public interface ISingleProcessLock : IDisposable
    {
        Task ReportAlive(string state, CancellationToken ct, bool clearHistory = false);
        Task Release(CancellationToken ct, bool clearHistory = false);
        CancellationToken InstallCancelled { get; }
        LockData CurrentLockData { get; }
    }
}
