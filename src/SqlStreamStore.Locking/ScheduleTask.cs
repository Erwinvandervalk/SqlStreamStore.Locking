using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public delegate IDisposable ScheduleTask(TimeSpan at, Func<CancellationToken, Task> action);
}