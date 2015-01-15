using System;

namespace VidyoIntegration.CommonLib.VidyoTypes
{
    public class EndpointUrlMissingException : Exception
    {
        public EndpointUrlMissingException(string message)
            : base(message)
        {
        }

        public EndpointUrlMissingException()
            : base()
        {
        }
    }
}