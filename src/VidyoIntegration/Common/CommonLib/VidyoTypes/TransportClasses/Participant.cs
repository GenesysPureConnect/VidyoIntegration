using Newtonsoft.Json;

namespace VidyoIntegration.CommonLib.VidyoTypes.TransportClasses
{
    public class Participant
    {
        public int EntityId { get; set; }
        public int ParticipantId { get; set; }
        public string EntityType { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}