namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public class ChatInteractionMediaTypeParameters : MediaTypeParameters
    {
        public override VideoConversationMediaType MediaType
        {
            get { return VideoConversationMediaType.Chat; }
        }

    }
}