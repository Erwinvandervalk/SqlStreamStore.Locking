using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public interface ILockStore
    {
        Task<LockData> Get(CancellationToken ct);
        Task Save(LockData lockData, CancellationToken ct);
    }
}