using ININ.IceLib.Interactions;
using VidyoIntegration.CommonLib.CicTypes;

namespace VidyoIntegration.CommonLib.ConversationTypes
{
    public class CallbackVideoConversationInitializationParameters : VideoConversationInitializationParameters
    {
        public override VideoConversationMediaType MediaType
        {
            get { return VideoConversationMediaType.Callback; }
            set { }
        }

        public string CallbackPhoneNumber { get; set; }
        public string CallbackMessage { get; set; }
    }
}