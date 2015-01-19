using VidyoIntegration.TraceLib;

namespace VidyoIntegration.VidyoAddin
{
    public class MainTopic : TopicTracer
    {
        public static int hdl = I3Trace.initialize_topic("VidyoAddin.Main", 80);

        public override int get_handle()
        {
            return hdl;
        }
    }

    public class Trace : TraceLibBase
    {
        public static MainTopic Main = new MainTopic();
    }
}
