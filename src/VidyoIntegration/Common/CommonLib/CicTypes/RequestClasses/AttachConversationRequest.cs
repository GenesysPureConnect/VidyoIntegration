using System.Collections.Generic;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;

namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public class AttachConversationRequest
    {
        public long InteractionId { get; set; }
        public List<KeyValuePair<string, string>> AdditionalAttributes { get; set; }
        public string GuestName { get; set; }
    }
}
