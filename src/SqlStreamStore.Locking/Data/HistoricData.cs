using System;

namespace SqlStreamStore.Locking.Data
{
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