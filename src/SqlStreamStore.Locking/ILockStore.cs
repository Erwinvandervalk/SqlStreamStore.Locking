using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public interface ILockStore
    {
        Task<LockData> Get(CancellationToken ct);
        Task Store(LockData currentHistory, CancellationToken ct);
    }
}