using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace VidyoIntegration.TraceLib
{
    public class TraceLibBase
    {
        /// <summary>
        /// Internal tracing topic used by TraceLib
        /// </summary>
        public static Topic Core = new Topic("TraceLib.Core", 80);

        internal static string EventSource = "TraceLibDefaultSource";
        internal static string EventLogName = "Application";

        internal static Dictionary<int, RegisteredMessage> RegisteredMessages = new Dictionary<int, RegisteredMessage>();

        /// <summary>
        /// Initializes tracing with the default event source and default event IDs
        /// </summary>
        public static void Initialize()
        {
            Initialize(typeof (EventId), EventSource);
        }

        /// <summary>
        /// Initializes tracing with a custom event source
        /// </summary>
        /// <param name="eventIdSubclass">Custom event ID class</param>
        /// <param name="eventSource">Custom event source</param>
        public static void Initialize(Type eventIdSubclass, string eventSource)
        {
            SetEventLogSource(eventSource);
            I3Trace.initialize_default_sinks();
            RegisterEventMessages(eventIdSubclass);
            var application = Assembly.GetCallingAssembly();
            var thisAssembly = Assembly.GetAssembly(typeof(TraceLibBase));

            var appInfoBuilder = new StringBuilder();
            
            appInfoBuilder.AppendLine(String.Format("Application info: {0} [{1}]", application.GetName().Name, application.GetName().Version));
            appInfoBuilder.AppendLine(String.Format("TraceLib info: {0} [{1}]", thisAssembly.GetName().Name, thisAssembly.GetName().Version));
            appInfoBuilder.AppendLine(String.Format("User: {0}", Environment.UserName));
            appInfoBuilder.AppendLine(String.Format("Machine: {0}", Environment.MachineName));
            appInfoBuilder.AppendLine(String.Format("OS: {0}", Environment.OSVersion));
            appInfoBuilder.AppendLine(String.Format(".NET version: {0}", Environment.Version));
            appInfoBuilder.AppendLine(String.Format("Application directory: {0}", Environment.CurrentDirectory));

            WriteRegisteredMessage(EventId.ApplicationInfo, appInfoBuilder.ToString());
        }

        /// <summary>
        /// Dynamically registers event messages in a type by using reflection
        /// </summary>
        /// <param name="eventIdSubclass"></param>
        internal static void RegisterEventMessages(Type eventIdSubclass)
        {
            using (Core.scope())
            {
                try
                {
                    // Verify type is EventId or subclass thereof
                    if (!typeof (EventId).IsAssignableFrom(eventIdSubclass))
                    {
                        Core.error("Class for event registration \"{}\" was not of type VidyoIntegration.TraceLib.EventId or a subclass thereof. Defaulting to type VidyoIntegration.TraceLib.EventId.", eventIdSubclass);
                        eventIdSubclass = typeof (EventId);
                    }

                    foreach (var field in eventIdSubclass.GetFields(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static))
                    {

                        try
                        {
                            if (field.FieldType != typeof(int))
                            {
                                Core.status("Ignoring field: {}, because it is of type: {}", field.Name, field.FieldType);
                                continue;
                            }
                            if (!field.IsLiteral)
                            {
                                Core.status("Ignoring field: {}, because it not declared as const", field.Name);
                                continue;
                            }
                            if (!field.IsPublic)
                            {
                                Core.status("Ignoring field: {}, because it not declared as public", field.Name);
                                continue;
                            }
                            var attr = field.GetCustomAttributes(typeof (EventIdAttributes), true);
                            var eventAttr = attr as EventIdAttributes[];
                            if (eventAttr == null)
                            {
                                Core.status("Ignoring field: {}, because EventIdAttributes did not exist", field.Name);
                                continue;
                            }
                            if (eventAttr.Length == 0)
                            {
                                Core.status("Ignoring field: {}, because EventIdAttributes had no items in the array", field.Name);
                                continue;
                            }
                            if (eventAttr.Length > 1)
                            {
                                Core.status("Field {} has multiple items in the array. That's odd.", field.Name);
                                continue;
                            }
                            if (eventAttr[0].DoNotRegister)
                            {
                                Core.status("Ignoring field: {}, because DoNotRegister==true", field.Name);
                                continue;
                            }

                            RegisterEventMessage((int) field.GetRawConstantValue(), eventAttr[0].EventMessage,
                                                 eventAttr[0].EventType, eventAttr[0].SupportsCustomMessage);
                        }
                        catch (Exception ex)
                        {
                            WriteEventError(ex, "Exception caught processing field: " + field.Name, EventId.GenericError);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventError(ex, "Exception caught during dynamic event message registration.",
                                    EventId.GenericError);
                }
            }
        }

        /// <summary>
        /// Sets the 'source' for event messages. This should typically be your application's name. Note that this method
        /// will attempt to register the event log source if it does not exist, which requires admin privledges.
        /// </summary>
        /// <param name="source">The source name to use</param>
        public static void SetEventLogSource(string source)
        {
            try
            {
                // Validation
                if (string.IsNullOrEmpty(source)) return;
                if (string.Equals(source, EventSource)) return;

                Core.always("Changing event log source to {}", source);

                /* Fair warning, this will throw an exception when attempting to create the event log source 
                 * if the executing user does not have permission to do so. Desktop users will not have 
                 * permission unless they are running the application as administrator. As a best practice, 
                 * the event log source should ALWAYS be created by the installer.
                 */
                if (!EventLog.SourceExists(EventSource)) EventLog.CreateEventSource(EventSource, EventLogName);
                EventSource = source;
            }
            catch (Exception ex)
            {
                Core.exception(ex, "Exception caught attempting to change event log source: {}", ex.Message);
            }
        }

        /// <summary>
        /// Register an event Id for future use
        /// </summary>
        /// <param name="eventId">The event Id. Custom events must use event Id >= 5000</param>
        /// <param name="message">The message to log</param>
        /// <param name="entryType">The event type</param>
        /// <param name="supportsCustomMessage"><c>True</c> if the message supports a custom message inline via a single set of curly braces (like this: {}).</param>
        public static void RegisterEventMessage(int eventId, string message, EventLogEntryType entryType, bool supportsCustomMessage)
        {
            try
            {
                if (RegisteredMessages.ContainsKey(eventId)) throw new MessageIdAlreadyRegisteredException(eventId);
                Core.always("Registering event message\r\nEventId: {}\r\nMessage: {}\r\nEntryType: {}",
                            eventId, message, entryType);
                RegisteredMessages.Add(eventId,
                                       new RegisteredMessage
                                           {
                                               EventId = eventId,
                                               Message = message,
                                               EntryType = entryType,
                                               SupportsCustomMessage = supportsCustomMessage
                                           });
            }
            catch (Exception ex)
            {
                WriteEventError(ex, "Exception caught during event message registration.", EventId.GenericError);
                throw;
            }
        }


        /// <summary>
        /// Writes a message to the event log based on the registered event Id.
        /// </summary>
        /// <param name="eventId">The registered event Id.</param>
        public static void WriteRegisteredMessage(int eventId)
        {
            try
            {
                WriteRegisteredMessage(eventId, "", false);
            }
            catch (Exception ex)
            {
                WriteEventError(ex, ex.Message, EventId.GenericError);
            }
        }

        /// <summary>
        /// Writes a message to the event log based on the registered event Id.
        /// </summary>
        /// <param name="eventId">The registered event Id.</param>
        /// <param name="throwExceptions"><c>True</c> if exceptions should be thrown out of this method. Otherwise, <c>False</c>.</param>
        public static void WriteRegisteredMessage(int eventId, bool throwExceptions)
        {
            try
            {
                WriteRegisteredMessage(eventId, "", throwExceptions);
            }
            catch (Exception ex)
            {
                WriteEventError(ex, ex.Message, EventId.GenericError);
                if (throwExceptions) throw;
            }
        }

        /// <summary>
        /// Writes a message to the event log based on the registered event Id.
        /// </summary>
        /// <param name="eventId">The registered event Id.</param>
        /// <param name="customMessage">The custom message. Will overwrite the registered message if it does not support custom messages.</param>
        public static void WriteRegisteredMessage(int eventId, string customMessage)
        {
            try
            {
                WriteRegisteredMessage(eventId, customMessage, false);
            }
            catch (Exception ex)
            {
                WriteEventError(ex, ex.Message, EventId.GenericError);
            }
        }

        /// <summary>
        /// Writes a message to the event log based on the registered event Id.
        /// </summary>
        /// <param name="eventId">The registered event Id.</param>
        /// <param name="customMessage">The custom message. Will overwrite the registered message if it does not support custom messages.</param>
        /// <param name="throwExceptions"><c>True</c> if exceptions should be thrown out of this method. Otherwise, <c>False</c>.</param>
        public static void WriteRegisteredMessage(int eventId, string customMessage, bool throwExceptions)
        {
            try
            {
                if (!RegisteredMessages.ContainsKey(eventId)) throw new InvalidMessageIdException(eventId);
                var eventMessage = RegisteredMessages[eventId];
                string message = eventMessage.Message;
                if (!string.IsNullOrEmpty(customMessage) && eventMessage.SupportsCustomMessage)
                {
                    // Inject custom message
                    message = message.Replace("{}", customMessage);
                }
                else if (!string.IsNullOrEmpty(customMessage))
                {
                    // Overwrite existing message because the existing message doesn't support a custom message
                    message = customMessage;
                }
                else
                {
                    // Get rid of curly braces and continue on
                    message = message.Replace("{}", "").Trim();
                }
                WriteEventMessage(message, eventMessage.EntryType, eventMessage.EventId);
            }
            catch (Exception ex)
            {
                WriteEventError(ex, ex.Message, EventId.GenericError);
                if (throwExceptions) throw;
            }
        }

        /// <summary>
        /// Write Exception to Event Log
        /// </summary>
        /// <param name="exception">The Exception object</param>
        /// <param name="message">The message to log</param>
        /// <param name="eventId">The event Id. Custom events must use event Id >= 5000</param>
        public static void WriteEventError(Exception exception, string message, int eventId)
        {
            using (Core.scope())
            {
                try
                {
                    Core.exception(exception, message);
                    WriteEventMessage(String.Format("{0}{1}{2}", message, Environment.NewLine, exception.Message),
                                      EventLogEntryType.Error,
                                      eventId);
                }
                catch (Exception ex)
                {
                    Core.exception(ex);
                }
            }
        }

        /// <summary>
        /// Write Message to the Event Log
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="entryType">The event type</param>
        /// <param name="eventId">The event Id. Must use event Id >= 5000</param>
        public static void WriteEventMessage(string message, EventLogEntryType entryType, int eventId)
        {
            using (Core.scope())
            {
                try
                {
                    Core.status(
                        "Writing event message:" + Environment.NewLine +
                        "Source: {}" + Environment.NewLine +
                        "Log: {}" + Environment.NewLine +
                        "Type: {}" + Environment.NewLine +
                        "Message: {}" + Environment.NewLine,
                        new object[] { EventSource, EventLogName, entryType.ToString(), message });

                    EventLog.WriteEntry(EventSource, message, entryType, eventId);
                }
                catch (Exception ex)
                {
                    Core.exception(ex, "Exception caught during writing event message, Exception: {}", ex.Message);
                }
            }
        }
    }
}
