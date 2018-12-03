using System;

namespace SqlStreamStore.Locking
{
    public class TryAquireLockResult : IDisposable
    {
        public readonly bool Aquired;
        public readonly ISingleProcessLock AquiredLock;

        public TryAquireLockResult(bool aquired, ISingleProcessLock aquiredLock)
        {
            Aquired = aquired;
            AquiredLock = aquiredLock;
        }

        public void Dispose()
        {
            AquiredLock?.Dispose();
        }
    }
}