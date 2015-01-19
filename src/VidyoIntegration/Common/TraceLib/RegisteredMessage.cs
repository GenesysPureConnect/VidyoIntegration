using System;
using System.Diagnostics;

namespace VidyoIntegration.TraceLib
{
    internal struct RegisteredMessage
    {
        internal int EventId;
        internal string Message;
        internal EventLogEntryType EntryType;
        internal bool SupportsCustomMessage;
    }

    public class MessageIdAlreadyRegisteredException : Exception
    {
        private MessageIdAlreadyRegisteredException() : base()
        {
        }

        public MessageIdAlreadyRegisteredException(int eventId)
            : base("The message id " + eventId + " has already been registered.")
        {
        }
    }

    public class InvalidMessageIdException : Exception
    {
        private InvalidMessageIdException()
            : base()
        {
        }

        public InvalidMessageIdException(int eventId)
            : base("The message id " + eventId + " has not been registered.")
        {
        }
    }

}
