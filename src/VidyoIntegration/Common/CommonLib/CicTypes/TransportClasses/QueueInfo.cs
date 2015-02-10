using System;

namespace VidyoIntegration.CommonLib.CicTypes.TransportClasses
{
    public class QueueInfo
    {
        public string QueueName { get; set; }
        public int InteractionCount { get; set; }
        public int NumberAvailableForAcdInteractions { get; set; }
        public double PercentAvailable { get; set; }
        public TimeSpan AverageWaitTimeCurrentPeriod { get; set; }
        public TimeSpan AverageWaitTimePreviousPeriod { get; set; }
        public TimeSpan AverageWaitTimeCurrentShift { get; set; }
        public TimeSpan AverageWaitTimePreviousShift { get; set; }
        public int InteractionsWaiting { get; set; }
    }
}
