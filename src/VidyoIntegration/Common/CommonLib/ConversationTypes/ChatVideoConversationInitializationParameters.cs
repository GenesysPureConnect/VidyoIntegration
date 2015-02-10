using ININ.IceLib.Interactions;
using VidyoIntegration.CommonLib.CicTypes;

namespace VidyoIntegration.CommonLib.ConversationTypes
{
    public class ChatVideoConversationInitializationParameters : VideoConversationInitializationParameters
    {
        public override VideoConversationMediaType MediaType
        {
            get { return VideoConversationMediaType.Chat; }
            set { }
        }
    }
}