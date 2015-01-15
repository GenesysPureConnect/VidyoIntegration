using ININ.IceLib.Interactions;
using Newtonsoft.Json;
using VidyoIntegration.CommonLib.CicTypes;

namespace VidyoIntegration.CommonLib.ConversationTypes
{
    public class GenericInteractionVideoConversationInitializationParameters : VideoConversationInitializationParameters
    {
        public override VideoConversationMediaType MediaType
        {
            get { return VideoConversationMediaType.GenericInteraction; }
            set { }
        }

        public GenericInteractionInitialState InitialState { get; set; }
    }
}