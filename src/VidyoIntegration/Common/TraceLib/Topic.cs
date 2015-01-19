namespace VidyoIntegration.TraceLib
{
    /// <summary>
    /// Topic initializer class allows for dynamic names so there isn't a new class for each topic
    /// </summary>
    public class Topic : TopicTracer
    {
        private readonly int _hdl;

        /// <summary>
        /// Initializes a new tracing topic
        /// </summary>
        /// <param name="name">The name of the topic as it will appear in the trace files</param>
        /// <param name="defaultLevel">The default tracing level, set when the topic is created and when the level is reset</param>
        public Topic(string name, int defaultLevel = 60)
        {
            _hdl = I3Trace.initialize_topic(name, defaultLevel);
        }

        /// <summary>
        /// Gets the handle for the trace topic.  Used by I3Trace
        /// </summary>
        /// <returns>Returns the handle for the trace topic</returns>
        public override int get_handle()
        {
            return _hdl;
        }
    }
}
