using System;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore.Locking.Data;

namespace SqlStreamStore.Locking
{
    public class LockManager
    {
        private readonly ILockStore _lockStore;
        private readonly Options _options;
        private readonly ScheduleRecurring _scheduleRecurring;

        public LockManager(IStreamStore streamStore, string streamName = "sqlStreamStore.locking", Options options = null) : this(new LockStore(streamStore, streamName), options, DefaultScheduler.ScheduleRecurring)
        {

        }

        public static LockManager BuildLockManager(ILockStore lockStore,
            Options options = null, ScheduleRecurring scheduleRecurring = null)
        {
            return new LockManager(lockStore, options, scheduleRecurring ?? DefaultScheduler.ScheduleRecurring);
        }

        public LockManager(ILockStore lockStore, Options options) : this(lockStore, options, DefaultScheduler.ScheduleRecurring)
        {

        }

        private LockManager(ILockStore lockStore, Options options, ScheduleRecurring scheduleRecurring)
        {
            _lockStore = lockStore ?? throw new ArgumentNullException(nameof(lockStore));
            _options = options ?? Options.Default;
            if (scheduleRecurring != null) _scheduleRecurring = scheduleRecurring;
        }

        public async Task<TryAquireLockResult> TryAquireLock(CancellationToken ct, bool clearHistory = false)
        {
            // Query for existing lock
            var lockData = await _lockStore.Get(ct);

            if (lockData.CanAquireLock)
            {
                var acquiredLock = lockData.Acquired(_options.ClearHistoryOnAquire || clearHistory);
                await _lockStore.Save(acquiredLock, ct);
                return new TryAquireLockResult(true, BuildLock(acquiredLock, ct));
            }

            return new TryAquireLockResult(false, null);
        }

        public async Task<ISingleProcessLock> WaitUntilLockIsAquired(CancellationToken ct, bool clearHistory = false)
        {
            // Query for existing lock
            var lockData = await _lockStore.Get(ct);

            if (lockData.CanAquireLock)
            {
                var acquiredLock = lockData.Acquired(_options.ClearHistoryOnAquire || clearHistory);
                await _lockStore.Save(acquiredLock, ct);
                return BuildLock(acquiredLock, ct);
            }

            var tcs = new TaskCompletionSource<ISingleProcessLock>();
            var elapsed = TimeSpan.Zero;
            int currentVersion = lockData.Version;
            var stopScheduler = _scheduleRecurring(TimeSpan.FromSeconds(1), async (innerCt) =>
            {
                elapsed += TimeSpan.FromSeconds(1);
                lockData = await _lockStore.Get(innerCt);

                if (lockData.CanAquireLock || (lockData.Version == currentVersion && elapsed > _options.DbTimeout))
                {
                    // No progress, so we can safely take over
                    var acquiredLock = lockData.TakeOver(clearHistory);
                    await _lockStore.Save(acquiredLock, innerCt);
                    tcs.SetResult(BuildLock(acquiredLock, ct));
                }
                else
                {
                    currentVersion = lockData.Version;
                }
            }, ct);

            var result = await tcs.Task;
            await stopScheduler();
            return result;

        }

        private SingleProcessSingleProcessLock BuildLock(LockData acquiredLock, CancellationToken ct)
        {
            return new SingleProcessSingleProcessLock(_lockStore, acquiredLock, _scheduleRecurring, _options, ct);
        }

        public async Task<LockData> GetCurrentState(CancellationToken ct)
        {
            return await _lockStore.Get(ct);
        }

        public class Options
        {
            /// <summary>
            /// How long does it take for the lock to time out in the database. This can happen when the process is killed or completely hangs. 
            /// </summary>
            public readonly TimeSpan DbTimeout;

            /// <summary>
            /// How long does it take for the task to time out (without having keepalive called)
            /// </summary>
            public readonly TimeSpan TaskTimeout;

            /// <summary>
            /// How often should the task check for progress & timing out
            /// </summary>
            public readonly TimeSpan RefreshInterval;

            /// <summary>
            /// Should the history be cleared on aquiring (typically done if you don't need to keep the history)
            /// </summary>
            public bool ClearHistoryOnAquire;

            public static Options Default => new Options();

            public Options(TimeSpan? dbTimeout = null, TimeSpan? taskTimeout = null, TimeSpan? refreshInterval = null, bool clearHistoryOnAquire = false)
            {
                RefreshInterval = refreshInterval ?? TimeSpan.FromSeconds(5);
                TaskTimeout = taskTimeout ?? TimeSpan.FromSeconds(90);
                DbTimeout = dbTimeout ?? TimeSpan.FromTicks((TaskTimeout.Ticks * 3));

                if (DbTimeout <= TaskTimeout) throw new ArgumentOutOfRangeException($"The database timeout {DbTimeout} cannot be smaller than the task timeout {TaskTimeout}. ");
                if (TaskTimeout <= RefreshInterval) throw new ArgumentOutOfRangeException($"The task timeout {RefreshInterval} cannot be smaller than the refresh interval {RefreshInterval}. ");

                ClearHistoryOnAquire = clearHistoryOnAquire;
            }
        }
    }
}