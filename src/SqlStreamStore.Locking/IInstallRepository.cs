using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public interface IInstallRepository
    {
        Task<LockData> Get(CancellationToken ct);
        Task Remember(LockData currentHistory, CancellationToken ct, LockData.CompletedStep newlyCompletedStep = null);
    }
}