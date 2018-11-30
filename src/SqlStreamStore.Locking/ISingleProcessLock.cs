using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public interface ISingleProcessLock : IDisposable
    {
        Task ReportAliveAsync(CancellationToken ct);
        Task Release(CancellationToken ct);
        CancellationToken InstallCancelled { get; }
    }
}
