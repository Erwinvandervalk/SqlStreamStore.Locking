using System;

namespace SqlStreamStore.Locking
{
    public class TryAcquireLockResult : IDisposable
    {
        public readonly bool Acquired;
        public readonly ISingleProcessLock AcquiredLock;

        public TryAcquireLockResult(bool acquired, ISingleProcessLock acquiredLock)
        {
            Acquired = acquired;
            AcquiredLock = acquiredLock;
        }

        public void Dispose()
        {
            AcquiredLock?.Dispose();
        }
    }
}