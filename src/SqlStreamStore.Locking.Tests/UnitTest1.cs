using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SqlStreamStore.Locking.Tests
{
    public class UnitTest1
    {
        private readonly CancellationToken ct = CancellationToken.None;

        [Fact]
        public async Task Can_aquire_lock_and_release_it()
        {
            var delayer = new TestDelayer();
            var repo = new LockStore(new InMemoryStreamStore(), "msg");
            var installer = new LockManager(repo, delayer.SimulatedDelayBy);

            var aquiredLock = await installer.AquireSingleProcessLock(ct);

            await aquiredLock.Release(ct);
        }

        [Fact]
        public async Task A_lock_expires_automatically()
        {
            var delayer = new TestDelayer();
            var repo = new LockStore(new InMemoryStreamStore(), "msg");
            var installer = new LockManager(repo, delayer.SimulatedDelayBy);

            var aquiredLock = await installer.AquireSingleProcessLock(ct);

            await delayer.AdvanceBy(TimeSpan.FromSeconds(60));
            await Task.Delay(100);
            Assert.True(aquiredLock.InstallCancelled.IsCancellationRequested);
        }


        [Fact]
        public async Task Only_one_process_can_acquire_lock()
        {
            var delayer = new TestDelayer();
            var repo = new LockStore(new InMemoryStreamStore(), "msg");
            var installer = new LockManager(repo, delayer.SimulatedDelayBy);

            // Acquire a lock
            var acquiredLock = await installer.AquireSingleProcessLock(ct);

            // Start a second async task that attempts to acquire the lock
            // (use a tcs to ensure the task is running)
            var secondTaskStarted = new TaskCompletionSource<bool>();
            bool secondTaskCompleted = false;

            var secondLock = Task.Run(async () =>
            {
                var acquired = installer.AquireSingleProcessLock(ct);
                secondTaskStarted.SetResult(true);
                await acquired;
                secondTaskCompleted = true;
            }, ct);

            await secondTaskStarted.Task;
            await delayer.AdvanceBy(TimeSpan.FromSeconds(5));

            Assert.False(secondTaskCompleted);

            await acquiredLock.Release(ct);
            await delayer.AdvanceBy(TimeSpan.FromSeconds(5));

            await secondLock;
            Assert.True(secondTaskCompleted);
        }
    }

    public class TestDelayer
    {
        List<(TimeSpan fireAfter, TaskCompletionSource<bool>taskToFire)> _tasks = new List<(TimeSpan, TaskCompletionSource<bool>)>();

        TimeSpan _passedTime = TimeSpan.Zero;

        public Task SimulatedDelayBy(TimeSpan by, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();

            _tasks.Add((_passedTime + by, tcs));

            return tcs.Task;
        }

        public async Task AdvanceBy(TimeSpan timeSpan)
        {
            var incrementInMilliseconds = 1000;
            var ticks = timeSpan.TotalMilliseconds / incrementInMilliseconds;
            for(int tick = 0; tick < ticks; tick++)
            {
                await Tick(TimeSpan.FromMilliseconds(incrementInMilliseconds));
            }

            var remainder = timeSpan.TotalMilliseconds % incrementInMilliseconds;
            if (remainder > 0)
            {
                await Tick(TimeSpan.FromMilliseconds(remainder));
            }
        }

        private async Task Tick(TimeSpan timeSpan)
        {
            _passedTime = _passedTime + timeSpan;
            var toTrigger = _tasks.Where(x => x.fireAfter <= _passedTime).ToArray();
            foreach (var item in toTrigger)
            {
                _tasks.Remove(item);
                item.taskToFire.SetResult(true);
                await Task.Yield();
                await Task.Delay(1);
            }
        }
    }
}
