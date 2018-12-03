using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlStreamStore.Locking.Data
{
    public class LockData
    {
        public static LockData Unlocked(int version = -1)
        {
            return new LockData(version, null, new List<HistoricData>());
        }

        public bool CanAquireLock => !History.Any() || (
                                         History.Last().Action == LockAction.None
                                         || History.Last().Action == LockAction.Released
                                         || History.Last().Action == LockAction.TimedOut
                                         || History.Last().Action == LockAction.Cancelled);

        public LockData(int version, string state, IEnumerable<HistoricData> history)
        {
            Version = version;
            State = state;
            History = history?.ToArray() ?? Array.Empty<HistoricData>();
        }

        public int Version { get; }

        public string State { get; }

        public IReadOnlyList<HistoricData> History { get; }

        public LockData Renewed(bool clearHistory = false)
        {
            return new LockData(
                version: Version + 1, 
                state: State, 
                history: clearHistory
                    ? Enumerable.Empty<HistoricData>()
                    : History);
        }

        public LockData Acquired(bool clearHistory = false)
        {
            return new LockData(
                version: Version + 1, 
                state: State, 
                history: clearHistory 
                    ? Enumerable.Empty<HistoricData>() 
                    : History.Append(HistoricData.Build(Version, State, LockAction.Acquired)).ToList());
        }

        public LockData WithProgress(string state, bool clearHistory = false)
        {
            var nextVersion = Version + 1;

            var history = clearHistory
                ? Enumerable.Empty<HistoricData>()
                : History;

            return new LockData(
                version: nextVersion, 
                state: state, 
                history: history.Append(HistoricData.Build(nextVersion, state, LockAction.Acquired)));
        }

        public LockData AfterAction(LockAction action, bool clearHistory = false)
        {
            var nextVersion = Version + 1;

            var history = clearHistory
                ? Enumerable.Empty<HistoricData>()
                : History;

            return new LockData(
                version: nextVersion,
                state: State,
                history: history.Append(HistoricData.Build(nextVersion, State, action)));
        }

        public LockData TakeOver(bool clearHistory = false)
        {
            var nextVersion = Version + 1;

            return new LockData(
                version: nextVersion,
                state: State,
                history: clearHistory
                    ? Enumerable.Empty<HistoricData>()
                    : History.Append(HistoricData.Build(nextVersion, State, LockAction.TakenOver)));
        }
    }
}