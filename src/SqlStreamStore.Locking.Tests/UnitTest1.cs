using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SqlStreamStore.Locking.Tests
{
    public class UnitTest1
    {
        private readonly CancellationToken ct = CancellationToken.None;

        [Fact]
        public async Task Test1()
        {
            var repo = new InstallRepository(new InMemoryStreamStore(), "msg");
            var installer = new InstallationProgressManager(repo);

            var aquiredLock = await installer.AquireSingleProcessLock(ct);

            await installer.RememberLastCompletedInstallationStep("step", aquiredLock, ct);

            await aquiredLock.Release(ct);
        }
    }
}
