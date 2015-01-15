using System;

namespace VidyoIntegration.CommonLib.Exceptions
{
    public class ConversationNotFoundException : Exception
    {
        public ConversationNotFoundException(Guid conversationId)
            : base("Unable to find conversation with conversation ID " + conversationId)
        {
        }
        public ConversationNotFoundException(long interactionId)
            : base("Unable to find conversation for interaction ID " + interactionId)
        {
        }
    }
}
