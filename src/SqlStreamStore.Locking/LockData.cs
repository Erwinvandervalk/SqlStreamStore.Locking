using System;
using System.Collections.Generic;
using System.Linq;

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

        public LockData Renewed()
        {
            return new LockData(Version + 1, State, History);
        }

        public LockData Acquired()
        {
            return new LockData(Version + 1, State, History.Append(HistoricData.Build(Version, null, LockAction.Acquired)).ToList());
        }

        public LockData WithProgress(string state)
        {
            var nextVersion = Version + 1;

            return new LockData(
                version: nextVersion, 
                state: state, 
                history: History.Append(HistoricData.Build(nextVersion, state, LockAction.Acquired)));
        }

        public LockData AfterAction(LockAction action)
        {
            var nextVersion = Version + 1;

            return new LockData(
                version: nextVersion,
                state: State,
                history: History.Append(HistoricData.Build(nextVersion, State, action)));
        }

        public LockData TakeOver()
        {
            var nextVersion = Version + 1;

            return new LockData(
                version: nextVersion,
                state: State,
                history: History.Append(HistoricData.Build(nextVersion, State, LockAction.TakenOver)));
        }
    }

    public class HistoricData
    {
        public static HistoricData Build(int version, string state, LockAction action)
        {
            return new HistoricData(version, state, action, DateTime.UtcNow, Environment.MachineName, Environment.UserName);
        }

        public HistoricData(int version, string state, LockAction action, DateTime atUtc, string onMachine, string byUser)
        {
            Version = version;
            State = state;
            Action = action;
            AtUtc = atUtc;
            OnMachine = onMachine;
            ByUser = byUser;
        }

        public int Version { get; }
        public string State { get; set; }
        public LockAction Action { get; }

        public DateTime AtUtc { get; }
        public string OnMachine { get; }
        public string ByUser { get; }



    }

}