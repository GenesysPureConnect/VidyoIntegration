using System.Collections.Generic;

namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public abstract class MediaTypeParameters
    {
        public List<KeyValuePair<string, string>> AdditionalAttributes { get; set; }
        public abstract VideoConversationMediaType MediaType { get; }
    }
}