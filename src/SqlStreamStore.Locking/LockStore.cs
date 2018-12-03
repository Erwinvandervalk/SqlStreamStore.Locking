using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlStreamStore.Locking.Data;
using SqlStreamStore.Streams;

namespace SqlStreamStore.Locking
{
    public class LockStore : ILockStore
    {
        private readonly IStreamStore _streamStore;
        private readonly string _streamId;
        private bool _maxLengthVerified;
        private int _maxCount = 5;

        public LockStore(IStreamStore streamStore, string streamId)
        {
            _streamStore = streamStore;
            _streamId = streamId;
        }

        public int MaxCount
        {
            get => _maxCount;
            set
            {
                _maxCount = value;
                _maxLengthVerified = false;
            }
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
            if (!_maxLengthVerified)
            {
                var metaData = await _streamStore.GetStreamMetadata(_streamId, ct);
                if (metaData.MaxCount != _maxCount)
                {
                    // We store a maximum of 5 rows in the db
                    await _streamStore.SetStreamMetadata(_streamId, maxAge: 5, cancellationToken: ct);
                }

                _maxLengthVerified = true;
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