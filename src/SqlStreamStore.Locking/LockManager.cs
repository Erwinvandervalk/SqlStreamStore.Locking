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

        public LockManager(IStreamStore streamStore, string streamName = "sqlStreamStore.locking",
            Options options = null) : this(new LockStore(streamStore, streamName), options,
            DefaultScheduler.ScheduleRecurring)
        {
        }

        public LockManager(ILockStore lockStore, Options options) : this(lockStore, options,
            DefaultScheduler.ScheduleRecurring)
        {
        }

        private LockManager(ILockStore lockStore, Options options, ScheduleRecurring scheduleRecurring)
        {
            _lockStore = lockStore ?? throw new ArgumentNullException(nameof(lockStore));
            _options = options ?? Options.Default;
            if (scheduleRecurring != null) _scheduleRecurring = scheduleRecurring;
        }

        public static LockManager BuildLockManager(ILockStore lockStore,
            Options options = null, ScheduleRecurring scheduleRecurring = null)
        {
            return new LockManager(lockStore, options, scheduleRecurring ?? DefaultScheduler.ScheduleRecurring);
        }

        public async Task<TryAcquireLockResult> TryAcquireLock(CancellationToken ct, bool clearHistory = false)
        {
            // Query for existing lock
            var lockData = await _lockStore.Get(ct);

            if (lockData.CanAcquireLock)
            {
                var acquiredLock = lockData.Acquired(_options.ClearHistoryOnAcquire || clearHistory);
                await _lockStore.Save(acquiredLock, ct);
                return new TryAcquireLockResult(true, BuildLock(acquiredLock, ct));
            }

            return new TryAcquireLockResult(false, null);
        }

        public async Task<ISingleProcessLock> WaitUntilLockIsAcquired(CancellationToken ct, bool clearHistory = false)
        {
            // Query for existing lock
            var lockData = await _lockStore.Get(ct);

            if (lockData.CanAcquireLock)
            {
                var acquiredLock = lockData.Acquired(_options.ClearHistoryOnAcquire || clearHistory);
                await _lockStore.Save(acquiredLock, ct);
                return BuildLock(acquiredLock, ct);
            }

            var tcs = new TaskCompletionSource<ISingleProcessLock>();
            var elapsed = TimeSpan.Zero;
            var currentVersion = lockData.Version;
            var stopScheduler = _scheduleRecurring(TimeSpan.FromSeconds(1), async innerCt =>
            {
                elapsed += TimeSpan.FromSeconds(1);
                lockData = await _lockStore.Get(innerCt);

                if (lockData.CanAcquireLock || lockData.Version == currentVersion && elapsed > _options.DbTimeout)
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

        private SingleProcessLock BuildLock(LockData acquiredLock, CancellationToken ct)
        {
            return new SingleProcessLock(_lockStore, acquiredLock, _scheduleRecurring, _options, ct);
        }

        public async Task<LockData> GetCurrentState(CancellationToken ct)
        {
            return await _lockStore.Get(ct);
        }

        public class Options
        {
            /// <summary>
            ///     How long does it take for the lock to time out in the database. This can happen when the process is killed or
            ///     completely hangs.
            /// </summary>
            public readonly TimeSpan DbTimeout;

            /// <summary>
            ///     How often should the task check for progress & timing out
            /// </summary>
            public readonly TimeSpan RefreshInterval;

            /// <summary>
            ///     How long does it take for the task to time out (without having keepalive called)
            /// </summary>
            public readonly TimeSpan TaskTimeout;

            /// <summary>
            ///     Should the history be cleared on acquiring (typically done if you don't need to keep the history)
            /// </summary>
            public bool ClearHistoryOnAcquire;

            public Options(TimeSpan? dbTimeout = null, TimeSpan? taskTimeout = null, TimeSpan? refreshInterval = null,
                bool clearHistoryOnAcquire = false)
            {
                RefreshInterval = refreshInterval ?? TimeSpan.FromSeconds(5);
                TaskTimeout = taskTimeout ?? TimeSpan.FromSeconds(90);
                DbTimeout = dbTimeout ?? TimeSpan.FromTicks(TaskTimeout.Ticks * 3);

                if (DbTimeout <= TaskTimeout)
                    throw new ArgumentOutOfRangeException(
                        $"The database timeout {DbTimeout} cannot be smaller than the task timeout {TaskTimeout}. ");
                if (TaskTimeout <= RefreshInterval)
                    throw new ArgumentOutOfRangeException(
                        $"The task timeout {RefreshInterval} cannot be smaller than the refresh interval {RefreshInterval}. ");

                ClearHistoryOnAcquire = clearHistoryOnAcquire;
            }

            public static Options Default => new Options();
        }
    }
}