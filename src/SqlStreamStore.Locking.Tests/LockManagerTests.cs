using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SqlStreamStore.Locking.Tests
{
    public class LockManagerTests
    {
        public LockManagerTests()
        {
            _lockStore = new LockStore(_inMemoryStreamStore, "msg");
            _sut = LockManager.BuildLockManager(_lockStore, UsedOptions, _scheduler.ScheduleRecurring);
        }

        private readonly CancellationToken ct = CancellationToken.None;
        private readonly TestScheduler _scheduler = new TestScheduler();
        private readonly LockStore _lockStore;
        private readonly InMemoryStreamStore _inMemoryStreamStore = new InMemoryStreamStore();
        private readonly LockManager _sut;

        public LockManager.Options UsedOptions = LockManager.Options.Default;

        public TimeSpan TimeAfterWhichTaskShouldTimeout => UsedOptions.TaskTimeout + TimeSpan.FromSeconds(5);
        public TimeSpan TimeAfterWhichDbShouldTimeout => UsedOptions.DbTimeout + TimeSpan.FromSeconds(5);
        public TimeSpan SafeTaskCheckingInterval => UsedOptions.RefreshInterval + TimeSpan.FromSeconds(1);

        [Fact]
        public async Task A_lock_expires_automatically()
        {
            using (var aquiredLock = await _sut.WaitUntilLockIsAquired(ct))
            {
                for (var i = 0; i < TimeAfterWhichDbShouldTimeout.TotalSeconds; i++)
                {
                    await _scheduler.AdvanceTimeBy(TimeSpan.FromSeconds(1));
                    if (aquiredLock.InstallCancelled.IsCancellationRequested)
                        break;
                }

                Assert.True(aquiredLock.InstallCancelled.IsCancellationRequested);
            }
        }

        [Fact]
        public async Task Can_aquire_lock_and_release_it()
        {
            using (var aquiredLock = await _sut.WaitUntilLockIsAquired(ct))
            {
                await aquiredLock.Release(ct);
                Assert.True(aquiredLock.InstallCancelled.IsCancellationRequested);
            }
        }

        [Fact]
        public async Task Can_keep_task_alive()
        {
            using (var aquiredLock = await _sut.WaitUntilLockIsAquired(ct))
            {
                for (var i = 0; i < 60; i++)
                {
                    await _scheduler.AdvanceTimeBy(TimeSpan.FromSeconds(1));
                    await aquiredLock.ReportAlive(null, aquiredLock.InstallCancelled);
                    if (aquiredLock.InstallCancelled.IsCancellationRequested)
                        break;
                }

                Assert.False(aquiredLock.InstallCancelled.IsCancellationRequested);
            }
        }

        [Fact]
        public async Task Can_take_over_lock_after_released()
        {
            using (var aquiredLock = await _sut.WaitUntilLockIsAquired(ct))
            {
                await aquiredLock.ReportAlive("state1", CancellationToken.None);
                await aquiredLock.Release(ct);
            }

            using (var aquiredLock = await _sut.WaitUntilLockIsAquired(ct))
            {
                Assert.Equal("state1", aquiredLock.CurrentLockData.State);
                await aquiredLock.ReportAlive("state2", CancellationToken.None);
                await aquiredLock.Release(ct);
            }

            var data = await _sut.GetCurrentState(CancellationToken.None);
            Assert.Equal("state2", data.State);
            Assert.Equal(new[] {null, "state1", "state1", "state1", "state2", "state2"},
                data.History.Select(x => x.State).ToArray());
            Assert.Equal(
                new[]
                {
                    LockAction.Acquired, LockAction.Acquired, LockAction.Released, LockAction.Acquired,
                    LockAction.Acquired, LockAction.Released
                }, data.History.Select(x => x.Action).ToArray());
        }

        [Fact]
        public async Task Can_try_acquire_lock()
        {
            using (var result = await _sut.TryAquireLock(ct))
            {
                Assert.True(result.Aquired);
                using (var secondAquire = await _sut.TryAquireLock(ct))
                {
                    Assert.False(secondAquire.Aquired);
                }

                // Disposing does not cause the lock in the DB to be released. If you don't explicitly
                // release it, it will time out eventually, but this is better
                await result.AquiredLock.Release(ct);
            }

            using (var thirdAquire = await _sut.TryAquireLock(ct))
            {
                Assert.True(thirdAquire.Aquired);
            }
        }


        [Fact]
        public async Task Only_one_process_can_acquire_lock()
        {
            // Acquire a lock
            var acquiredLock = await _sut.WaitUntilLockIsAquired(ct);

            // Start a second async task that attempts to acquire the lock
            // (use a tcs to ensure the task is running)
            var secondTaskStarted = new TaskCompletionSource<bool>();
            var secondTaskCompleted = false;

            var secondLock = Task.Run(async () =>
            {
                // This task tries to aquire lock.. it will keep waiting until aquired.
                var acquired = _sut.WaitUntilLockIsAquired(ct);
                secondTaskStarted.SetResult(true);
                await acquired;
                secondTaskCompleted = true;
            }, ct);

            // Ensure the second task is really running
            await secondTaskStarted.Task;

            // Advance the scheduler by 5 seconds.. this should not trigger
            // The second task to complete
            await _scheduler.AdvanceTimeBy(SafeTaskCheckingInterval);
            Assert.False(secondTaskCompleted);

            // Then release the lock. This should allow the second task to aquire the lock
            await acquiredLock.Release(ct);
            await _scheduler.AdvanceTimeBy(SafeTaskCheckingInterval);

            // Becuase the second task has aquired the lock, this should work normally 
            // But with a guard against timing out tests. 
            await Task.WhenAny(secondLock, Task.Delay(1000, ct));
            Assert.True(secondTaskCompleted);
        }

        [Fact]
        public async Task Will_cancel_task_when_needed()
        {
            var cancelled = false;
            var ranToCompletion = false;
            using (var aquiredLock = await _sut.WaitUntilLockIsAquired(ct))
            {
                var job = Task.Run(async () =>
                {
                    try
                    {
                        // it should not really wait for 1000 seconds. but 1000 seconds should be enough for the
                        // test to be killed. If not, then the 'ran to completion' will kill it. 
                        await Task.Delay(TimeSpan.FromSeconds(1000), aquiredLock.InstallCancelled);
                        ranToCompletion = true;
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                    }
                });

                await _scheduler.AdvanceTimeBy(TimeAfterWhichTaskShouldTimeout);

                await job;
                Assert.True(cancelled);
                Assert.False(ranToCompletion);
            }
        }
    }
}