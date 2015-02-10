using System.Collections.Generic;

namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public class GetQueueInfoRequest
    {
        public List<string> Queues { get; set; }
        public bool WaitForData { get; set; }
    }
}
