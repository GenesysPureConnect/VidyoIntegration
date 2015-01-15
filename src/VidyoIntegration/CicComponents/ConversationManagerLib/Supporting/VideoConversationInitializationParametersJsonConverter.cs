using System;
using Newtonsoft.Json.Linq;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.CicTypes.Serializers;
using VidyoIntegration.CommonLib.ConversationTypes;

namespace VidyoIntegration.ConversationManagerLib.Supporting
{
    public class VideoConversationInitializationParametersJsonConverter : AbstractJsonConverter<VideoConversationInitializationParameters>
    {
        protected override VideoConversationInitializationParameters Create(Type objectType, JObject jObject)
        {
            try
            {
                JToken token;
                if (jObject.TryGetValue("MediaType", StringComparison.InvariantCultureIgnoreCase, out token))
                {
                    var value =
                        (VideoConversationMediaType)
                            Enum.Parse(typeof (VideoConversationMediaType), token.Value<string>());
                    switch (value)
                    {
                        case VideoConversationMediaType.GenericInteraction:
                            return new GenericInteractionVideoConversationInitializationParameters();
                        case VideoConversationMediaType.Chat:
                            return new ChatVideoConversationInitializationParameters();
                        case VideoConversationMediaType.Callback:
                            return new CallbackVideoConversationInitializationParameters();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Trace.Conversation.exception(ex);
                return null;
            }
        }
    }
}
