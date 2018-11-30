using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlStreamStore.Streams;
using StreamStoreStore.Json;

namespace SqlStreamStore.Locking
{
    public class LockStore : ILockStore
    {
        private readonly IStreamStore _streamStore;
        private readonly string _streamId;

        public LockStore(IStreamStore streamStore, string streamId)
        {
            _streamStore = streamStore;
            _streamId = streamId;
        }

        public async Task<LockData> Get(CancellationToken ct)
        {
            var page = await _streamStore.ReadStreamBackwards(_streamId, StreamVersion.End, 1, prefetchJsonData: true,
                cancellationToken: ct);

            if (page.Messages.Length == 0)
                return LockData.Unlocked();

            var lastMessage = page.Messages.First();
            var data = await lastMessage.GetJsonData(ct);
            return JsonConvert.DeserializeObject<LockData>(data);
        }

        public async Task Save(LockData lockData, CancellationToken ct)
        {
            var metaData = await _streamStore.GetStreamMetadata(_streamId, ct);
            if (metaData.MaxCount != 5)
            {
                await _streamStore.SetStreamMetadata(_streamId, maxAge: 5, cancellationToken: ct);
            }

            var newData = JsonConvert.SerializeObject(lockData, Formatting.Indented);

            var result = await _streamStore.AppendToStream(
                streamId: _streamId,
                expectedVersion: lockData.Version -1,
                message: new NewStreamMessage(Guid.NewGuid(), "lockData", newData, null),
                cancellationToken: ct);
        }
    }
}