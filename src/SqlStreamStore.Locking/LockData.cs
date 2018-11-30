using System;
using System.Collections.Generic;

namespace SqlStreamStore.Locking
{
    public class LockData
    {
        public int Version { get; set; } = -1;
        public string LastCompletedStep { get; set; }

        public IReadOnlyList<CompletedStep> History { get; set; } = new List<CompletedStep>();

        public class CompletedStep
        {
            public static CompletedStep FromEnvironment(int version, string stepName, string message)
            {
                return new CompletedStep()
                {
                    Version = version,
                    StepName = stepName,
                    Message = message,
                    CompletedAtUtc = DateTime.UtcNow,
                    CompletedByUser = Environment.UserName,
                    CompletedOnMachine = Environment.MachineName
                };
            }

            public int Version { get; set; }
            public string StepName { get; set; }
            public string Message { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public string CompletedByUser { get; set; }
            public string CompletedOnMachine { get; set; }
        }
    }
}