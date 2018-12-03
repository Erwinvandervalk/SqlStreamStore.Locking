using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore.Locking.Data;

namespace SqlStreamStore.Locking
{
    public interface ILockStore
    {
        Task<LockData> Get(CancellationToken ct);
        Task Save(LockData lockData, CancellationToken ct);
    }
}