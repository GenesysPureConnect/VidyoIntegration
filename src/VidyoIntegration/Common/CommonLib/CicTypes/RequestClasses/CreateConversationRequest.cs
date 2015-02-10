using System.Collections.Generic;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;

namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public class CreateConversationRequest
    {
        public string QueueName { get; set; }
        public CicQueueType QueueType { get; set; }
        public MediaTypeParameters MediaTypeParameters { get; set; }

        public string GetScopedQueueName()
        {
            return (QueueType == CicQueueType.User ? "User Queue:" : "Workgroup Queue:") + QueueName;
        }
    }
}
