using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public delegate Func<Task> ScheduleRecurring(TimeSpan scheduleEvery, Func<CancellationToken, Task> taskToSchedule, CancellationToken ct);

}