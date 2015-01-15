using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VidyoIntegration.CommonLib.CicTypes.Serializers;

namespace VidyoIntegrationTestConsole
{
    public class CustomJsonSerializer : JsonSerializer
    {
        public CustomJsonSerializer()
        {
            this.ContractResolver = new CamelCasePropertyNamesContractResolver();
            this.Formatting = Formatting.Indented;
            this.Converters.Add(new MediaTypeParametersJsonConverter());
        }
    }
}