namespace SqlStreamStore.Locking
{
    public enum LockAction
    {
        None = 0,
        Acquired = 1,
        Released = 2,
        TimedOut = 3,
        TakenOver = 4,
        Cancelled = 5
    }
}