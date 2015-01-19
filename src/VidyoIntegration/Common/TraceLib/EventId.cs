using System;
using System.Diagnostics;

namespace VidyoIntegration.TraceLib
{
    // ReSharper disable InconsistentNaming
    /// <summary>
    /// Standard Event Ids for Windows Event Logging.
    /// </summary>
    public class EventId
    {
        #region Generic Messages - 1K block
        // Standard Application Messages: 1000-1099
        [EventIdAttributes(EventMessage = "The application has initialized successfully. {}", EventType = EventLogEntryType.Information, SupportsCustomMessage = true)]
        public const int ApplicationInitialized = 1000;
        [EventIdAttributes(EventMessage = "The application has failed to initialize and will now shut down. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int ApplicationInitializationCriticalFailure = 1001;
        [EventIdAttributes(EventMessage = "The application has encountered errors during initialization, but will continue to run. {}", EventType = EventLogEntryType.Warning, SupportsCustomMessage = true)]
        public const int ApplicationInitializationWarning = 1002;

        // General Application Messages: 1900-1999
        [EventIdAttributes(EventMessage = "This is a generic message. {}", EventType = EventLogEntryType.Information, SupportsCustomMessage = true)]
        public const int GenericMessage = 1900;
        [EventIdAttributes(EventMessage = "This is a generic warning. {}", EventType = EventLogEntryType.Warning, SupportsCustomMessage = true)]
        public const int GenericWarning = 1901;
        [EventIdAttributes(EventMessage = "This is a generic error. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int GenericError = 1902;
        [EventIdAttributes(EventMessage = "Application Info. {}", EventType = EventLogEntryType.Information, SupportsCustomMessage = true)]
        public const int ApplicationInfo = 1903;
        #endregion

        #region IceLib - 2K block
        // CIC Connection Messages: 2000-2099
        [EventIdAttributes(EventMessage = "The connection to CIC has been established. {}", EventType = EventLogEntryType.Information, SupportsCustomMessage = true)]
        public const int CICConnectionLogOn = 2000;
        [EventIdAttributes(EventMessage = "Connection to CIC lost due to switchover. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int CICConnectionSwitchover = 2001;
        [EventIdAttributes(EventMessage = "Connection to CIC has timed out. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int CICConnectionSessionTimeOut = 2002;
        [EventIdAttributes(EventMessage = "Connection to CIC has been terminated because the user has been logged off remotely.", EventType = EventLogEntryType.Error)]
        public const int CICConnectionUserLogOff = 2003;
        [EventIdAttributes(EventMessage = "User authentication to CIC has failed. {}", EventType = EventLogEntryType.Warning, SupportsCustomMessage = true)]
        public const int CICConnectionLogOnFailed = 2004;
        [EventIdAttributes(EventMessage = "Connection to CIC has been terminated because the user has been logged off due to another log in elsewhere.", EventType = EventLogEntryType.Error)]
        public const int CICConnectionAnotherLogOn = 2005;
        [EventIdAttributes(EventMessage = "Connection to CIC lost due to server not responding. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int CICConnectionServerNotResponding = 2006;
        [EventIdAttributes(EventMessage = "Connection to CIC has been terminated because the user has been logged off by an administrator.", EventType = EventLogEntryType.Error)]
        public const int CICConnectionAdminLogOff = 2007;
        [EventIdAttributes(EventMessage = "The user's current station has been deactivated.", EventType = EventLogEntryType.Error)]
        public const int CICConnectionStationDeactivated = 2008;
        [EventIdAttributes(EventMessage = "The user's current station has been deleted.", EventType = EventLogEntryType.Error)]
        public const int CICConnectionStationDeleted = 2009;
        [EventIdAttributes(EventMessage = "The current user has been deleted.", EventType = EventLogEntryType.Error)]
        public const int CICConnectionUserDeleted = 2010;

        // General IceLib Messages: 2100-2199
        [EventIdAttributes(EventMessage = "General configuration error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibGeneralError = 2100;
        [EventIdAttributes(EventMessage = "User configuration error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibUserConfigurationError = 2101;
        [EventIdAttributes(EventMessage = "Workgroup configuration error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibWorkgroupConfigurationError = 2102;
        [EventIdAttributes(EventMessage = "Role configuration error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibRoleConfigurationError = 2103;
        [EventIdAttributes(EventMessage = "Station configuration error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibStationpConfigurationError = 2104;
        [EventIdAttributes(EventMessage = "Permissions configuration error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibPermissionsError = 2105;

        // IceLib Recorder Messages: 2200-2299
        [EventIdAttributes(EventMessage = "General Recorder error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int IceLibGeneralRecorderError = 2200;

        // IceLib Dialer Messages: 2300-2399
        [EventIdAttributes(EventMessage = "General Dialer error: {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int DialerGeneralError = 2300;
        #endregion

        #region Other Components - 3K and 4K blocks
        // Database Messages: 3000-3099
        [EventIdAttributes(EventMessage = "Database connection successful. {}", EventType = EventLogEntryType.Information, SupportsCustomMessage = true)]
        public const int DBConnectionSuccessful = 3000;
        [EventIdAttributes(EventMessage = "Database connection failed. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int DBConnectionFailed = 3001;
        [EventIdAttributes(EventMessage = "Database credentials are invalid. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int DBInvalidCredentials = 3002;
        [EventIdAttributes(EventMessage = "Database connection string is invalid. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int DBInvalidConnectionString = 3003;
        [EventIdAttributes(EventMessage = "Database query timeout. {}", EventType = EventLogEntryType.Warning, SupportsCustomMessage = true)]
        public const int DBQueryTimeout = 3004;
        [EventIdAttributes(EventMessage = "Database query canceled. {}", EventType = EventLogEntryType.Warning, SupportsCustomMessage = true)]
        public const int DBQueryCanceled = 3005;

        // Web Service Messages: 3100-3199


        // File Handling Messages: 3200-3299
        [EventIdAttributes(EventMessage = "File loaded successfully. {}", EventType = EventLogEntryType.Information, SupportsCustomMessage = true)]
        public const int FileLoadedSuccessfully = 3200;
        [EventIdAttributes(EventMessage = "File load failed. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int FileLoadFailed = 3201;

        // FTP Messages: 3300-3399
        [EventIdAttributes(EventMessage = "FTP connection could not be established. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int FTPCouldNotConnect = 3300;
        [EventIdAttributes(EventMessage = "FTP directory not available. {}", EventType = EventLogEntryType.Error, SupportsCustomMessage = true)]
        public const int FTPDirectoryNotAvailable = 3301;
        #endregion

        #region User Defined - 5K and greater
        // DO NOT USE - RESERVED FOR DEFINITION IN USER APPLICATION
        #endregion
    }
    // ReSharper restore InconsistentNaming


    /// <summary>
    /// Event Id attributes. Should be used in conjunction with VidyoIntegration.TraceLib.EventId and derived classes.
    /// </summary>
    public class EventIdAttributes : Attribute
    {
        /// <summary>
        /// The message for the event.
        /// </summary>
        public string EventMessage { get; set; }

        /// <summary>
        /// The EventLogEntryType type for the event. Defaults to Information.
        /// </summary>
        public EventLogEntryType EventType { get; set; }

        /// <summary>
        /// <c>True</c> if the message should not be automatically registered. Defaults to <c>False</c>.
        /// </summary>
        public bool DoNotRegister { get; set; }

        /// <summary>
        /// <c>True</c> if the message supports a custom message inline via a single set of curly braces (like this: {}). Defaults to <c>False</c>.
        /// </summary>
        public bool SupportsCustomMessage { get; set; }

        public EventIdAttributes()
        {
            EventMessage = "Unknown message";
            EventType = EventLogEntryType.Information;
            DoNotRegister = false;
            SupportsCustomMessage = false;
        }

        public EventIdAttributes(string eventMessage, EventLogEntryType eventType, bool doNotRegister, bool supportsCustomMessage)
        {
            EventMessage = eventMessage;
            EventType = eventType;
            DoNotRegister = doNotRegister;
            SupportsCustomMessage = supportsCustomMessage;
        }
    }
}
