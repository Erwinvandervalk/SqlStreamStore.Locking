using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore.Streams;
using StreamStoreStore.Json;

namespace SqlStreamStore.Locking
{
    public class InstallRepository : IInstallRepository
    {
        private readonly IStreamStore _streamStore;
        private readonly string _streamId;

        public InstallRepository(IStreamStore streamStore, string streamId)
        {
            _streamStore = streamStore;
            _streamId = streamId;
        }

        public async Task<LockData> Get(CancellationToken ct)
        {
            var page = await _streamStore.ReadStreamBackwards(_streamId, StreamVersion.End, 1, prefetchJsonData: true,
                cancellationToken: ct);

            if (page.Messages.Length == 0)
                return new LockData();

            var lastMessage = page.Messages.LastOrDefault();
            return await lastMessage.GetJsonDataAs<LockData>(cancellationToken: ct);
        }

        public async Task Remember(LockData currentHistory, CancellationToken ct, LockData.CompletedStep newlyCompletedStep = null)
        {
            var metaData = await _streamStore.GetStreamMetadata(_streamId, ct);
            if (metaData.MaxCount != 5)
            {
                await _streamStore.SetStreamMetadata(_streamId, maxAge: 5, cancellationToken: ct);
            }

            var history = currentHistory.History.ToList();
            if (newlyCompletedStep != null)
            {
                history.Append(newlyCompletedStep);
            }

            var newData = SimpleJson.SerializeObject(new LockData
            {
                History = history,
                LastCompletedStep = newlyCompletedStep?.StepName ?? currentHistory.LastCompletedStep,
                Version = currentHistory.Version + 1
            });

            var result = await _streamStore.AppendToStream(
                streamId: _streamId,
                expectedVersion: currentHistory.Version,
                message: new NewStreamMessage(Guid.NewGuid(), "installhistory", newData, null),
                cancellationToken: ct);
        }
    }
}