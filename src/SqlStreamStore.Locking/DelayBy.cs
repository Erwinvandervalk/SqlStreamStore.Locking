using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlStreamStore.Locking
{
    public delegate Task DelayBy(TimeSpan timeSpan, CancellationToken ct);
}