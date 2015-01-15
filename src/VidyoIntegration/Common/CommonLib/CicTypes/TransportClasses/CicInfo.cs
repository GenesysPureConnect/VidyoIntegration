using System;
using System.Collections.Generic;

namespace VidyoIntegration.CommonLib.CicTypes.TransportClasses
{
    public class CicInfo
    {
        public int ConversationCount { get; set; }
        public bool IsConnectedToCic { get; set; }
        public string ConnectionMessage { get; set; }
        public string CicServer { get; set; }
        public string CicUser { get; set; }
        public TimeSpan Uptime { get; set; }
        public string SessionManager { get; set; }
        public Dictionary<string, int> RequestCounts { get; set; }
    }
}
