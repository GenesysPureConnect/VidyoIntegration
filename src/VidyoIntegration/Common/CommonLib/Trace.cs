using System.Diagnostics;
using VidyoIntegration.TraceLib;

namespace VidyoIntegration.CommonLib
{
    public class MainTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoIntegration.Main", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }
    public class VidyoTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoIntegration.Vidyo", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }
    public class ConfigTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoIntegration.Config", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }
    public class CicTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoIntegration.CIC", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }
    public class CommonTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoIntegration.Common", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }
    public class ConversationTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoIntegration.Conversation", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }

    public class Trace : TraceLibBase
    {
        public static MainTopic Main = new MainTopic();
        public static VidyoTopic Vidyo = new VidyoTopic();
        public static ConfigTopic Config = new ConfigTopic();
        public static CicTopic Cic = new CicTopic();
        public static CommonTopic Common = new CommonTopic();
        public static ConversationTopic Conversation = new ConversationTopic();
    }

    public class VidyoEventId : EventId
    {
        [EventIdAttributes(EventMessage = "The application has shut down.", EventType = EventLogEntryType.Information)]
        public const int ApplicationShutdown = 5000;
        
    }
}
