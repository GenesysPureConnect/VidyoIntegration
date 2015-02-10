namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public class CallbackInteractionMediaTypeParameters : MediaTypeParameters
    {
        public string CallbackPhoneNumber { get; set; }
        public string CallbackMessage { get; set; }

        public override VideoConversationMediaType MediaType
        {
            get { return VideoConversationMediaType.Callback; }
        }
    }
}
