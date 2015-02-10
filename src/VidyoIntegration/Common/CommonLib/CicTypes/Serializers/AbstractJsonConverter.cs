using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VidyoIntegration.CommonLib.CicTypes.RequestClasses;

namespace VidyoIntegration.CommonLib.CicTypes.Serializers
{
    public abstract class AbstractJsonConverter<T>:JsonConverter
    {
        protected abstract T Create(Type objectType, JObject jObject);

        public override bool CanConvert(Type objectType)
        {
            if (objectType == null) return false;
            return typeof(T).IsAssignableFrom(objectType);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            T target = Create(objectType, jObject);
            serializer.Populate(jObject.CreateReader(), target);

            return target;
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        protected static bool FieldExists(
            JObject jObject,
            string name,
            JTokenType type)
        {
            JToken token;
            return jObject.TryGetValue(name, out token) && token.Type == type;
        }
    }

    public class MediaTypeParametersJsonConverter :
        AbstractJsonConverter<MediaTypeParameters>
    {
        protected override MediaTypeParameters Create(Type objectType, JObject jObject)
        {
            JToken token;
            if (jObject.TryGetValue("MediaType", StringComparison.InvariantCultureIgnoreCase, out token))
            {
                //var value = token.Value<VideoConversationMediaType>();
                var value = (VideoConversationMediaType)Enum.Parse(typeof (VideoConversationMediaType), token.Value<string>());
                switch (value)
                {
                    case VideoConversationMediaType.GenericInteraction:
                        return new GenericInteractionMediaTypeParameters();
                    case VideoConversationMediaType.Chat:
                        return new ChatInteractionMediaTypeParameters();
                    case VideoConversationMediaType.Callback:
                        return new CallbackInteractionMediaTypeParameters();
                }
            }

            return null;
        }
    }
}
