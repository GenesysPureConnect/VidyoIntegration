using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ININ.IceLib.Connection;
using ININ.IceLib.Connection.Extensions;
using ININ.IceLib.Interactions;
using VidyoIntegration.TraceLib;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.CicTypes.TransportClasses;
using VidyoIntegration.CommonLib.ConversationTypes;

namespace VidyoIntegration.CicManagerLib
{
    public class CicManager
    {
        #region Private
        
        private readonly Session _session = new Session();
        private StatisticsWrapper _statisticsWrapper;
        private CustomNotification _customNotification;
        private readonly List<Interaction> _interactions = new List<Interaction>();
        private readonly Dictionary<long, string> _interactionAssignments = new Dictionary<long, string>();
        private string[] _watchedAttributes = {};

        private string[] WatchedAttributes
        {
            get
            {
                // Return list
                if (_watchedAttributes.Length > 0) return _watchedAttributes;

                // Create list
                _watchedAttributes = new string[_cicProtectedAttributes.Length + _vidyoWatchedAttributes.Length];
                _cicProtectedAttributes.CopyTo(_watchedAttributes, 0);
                _vidyoWatchedAttributes.CopyTo(_watchedAttributes, _cicProtectedAttributes.Length);
                return _watchedAttributes;
            }
        }

        private readonly string[] _cicProtectedAttributes =
        {
            InteractionAttributeName.InteractionId,
            InteractionAttributeName.CallIdKey,
            InteractionAttributeName.State,
            InteractionAttributeName.UserQueueNames,
            InteractionAttributeName.WorkgroupQueueName
        };

        private readonly string[] _vidyoWatchedAttributes =
        {
            VideoIntegrationAttributeNames.VideoConversationId,
            VideoIntegrationAttributeNames.VideoLastAgentToScreenPop
        };

        #endregion



        #region Public

        public const string VidyoGuestLinkRequestOid = "VidyoGuestLinkRequest";
        public const string VidyoGuestLinkRequestEid = "VidyoGuestLinkRequest";
        public const string VidyoNewConversationRequestOid = "VidyoNewConversationRequest";
        public const string VidyoNewConversationRequestEid = "VidyoNewConversationRequest";
        public const string VidyoGuestLinkResponseEid = "VidyoGuestLinkResponse";
        public const string VidyoServiceClientBaseUrlRequestOid = "VidyoServiceClientBaseUrlRequest";
        public const string VidyoServiceClientBaseUrlRequestEid = "VidyoServiceClientBaseUrlRequest";
        public const string VidyoServiceClientBaseUrlResponseEid = "VidyoServiceClientBaseUrlResponse";
        
        public bool IsConnected { get { return _session.ConnectionState == ConnectionState.Up; } }

        public string ConnectionMessage
        {
            get
            {
                return _session.ConnectionState + " [" + _session.ConnectionStateReason + "] - " +
                       _session.ConnectionStateMessage;
            }
        }

        public string CicServer { get { return _session.ICServer; } }

        public string SessionManager { get { return _session.SessionManagerNameFQDN; } }

        public string CicUser { get { return _session.UserId; } }

        public ReadOnlyCollection<long> Interactions
        {
            get { return new ReadOnlyCollection<long>(_interactions.Select(i => i.InteractionId.Id).ToList()); }
        }

        #endregion



        #region Eventing

        public delegate void InteractionAssignedHandler(long interactionId, string agentName);

        public event InteractionAssignedHandler InteractionAssigned;

        public delegate void InteractionDisconnectedHandler(long interactionId);

        public event InteractionDisconnectedHandler InteractionDisconnected;

        public delegate void InteractionChangedHandler(long interactionId, IDictionary<string, string> attributes);

        public event InteractionChangedHandler InteractionChanged;

        public delegate void InteractionQueueChangedHandler(long interactionId, string scopedQueueName, string userOwner);

        public event InteractionQueueChangedHandler InteractionQueueChanged;

        public delegate void SessionConnectionLostHandler();

        public event SessionConnectionLostHandler SessionConnectionLost;

        public delegate void SessionConnectionUpHandler();

        public event SessionConnectionUpHandler SessionConnectionUp;

        public delegate void VidyoNewConversationRequestedHandler(string username, string interactionId);

        public event VidyoNewConversationRequestedHandler VidyoNewConversationRequested;

        #endregion



        #region Constructor

        public CicManager()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    _session.ConnectionStateChanged += SessionOnConnectionStateChanged;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error constructing CicWrapper: " + ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion



        #region IceLib Methods

        private void SessionOnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    Console.WriteLine("CIC connection state changed: {0} ({1})", e.State, e.Reason);

                    if (e.State == ConnectionState.Down)
                    {
                        // Log event message when logon fails
                        switch (e.Reason)
                        {
                            case ConnectionStateReason.ServerNotResponding:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionServerNotResponding);
                                break;
                            case ConnectionStateReason.LogOnFailed:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionLogOnFailed);
                                break;
                            case ConnectionStateReason.AdminLogOff:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionAdminLogOff);
                                break;
                            case ConnectionStateReason.UserDeleted:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionUserDeleted);
                                break;
                            case ConnectionStateReason.StationDeleted:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionStationDeleted);
                                break;
                            case ConnectionStateReason.StationDeactivated:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionStationDeactivated);
                                break;
                            case ConnectionStateReason.AnotherLogOn:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionAnotherLogOn);
                                break;
                            case ConnectionStateReason.UserLogOff:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionUserLogOff);
                                break;
                            case ConnectionStateReason.SessionTimeout:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionSessionTimeOut);
                                break;
                            case ConnectionStateReason.Switchover:
                                Trace.WriteRegisteredMessage(EventId.CICConnectionSwitchover);
                                break;
                            default:
                                Trace.WriteRegisteredMessage(EventId.GenericError, "Connection to CIC is down! Reason: " + e.Reason);
                                break;
                        }

                        // Spread the word
                        Trace.Cic.warning("CIC connection lost! {}", e.Reason);
                        RaiseSessionConnectionLost();

                        // Clean up
                        if (_customNotification != null && _customNotification.IsWatching()) _customNotification.StopWatching();
                        _customNotification = null;

                        _statisticsWrapper.Dispose();

                        foreach (var interaction in _interactions)
                        {
                            try
                            {
                                interaction.StopWatching();
                            }
                            catch (Exception ex)
                            {
                                // Don't care
                            }
                        }
                        _interactions.Clear();
                        _interactionAssignments.Clear();
                    }

                    if (e.State != ConnectionState.Up)
                        return;

                    Trace.WriteRegisteredMessage(EventId.CICConnectionLogOn);

                    // Initialize when connected
                    Trace.Cic.note("Initializng stuff...");
                    _customNotification = new CustomNotification(_session);
                    _customNotification.CustomNotificationReceived += OnCustomNotificationReceived;
                    _customNotification.StartWatching(new[]
                    {
                        new CustomMessageHeader(
                            CustomMessageType.ApplicationRequest,
                            VidyoGuestLinkRequestOid,
                            VidyoGuestLinkRequestEid),
                        new CustomMessageHeader(
                            CustomMessageType.ApplicationRequest,
                            VidyoNewConversationRequestOid,
                            VidyoNewConversationRequestEid),
                        new CustomMessageHeader(
                            CustomMessageType.ApplicationRequest,
                            VidyoServiceClientBaseUrlRequestOid,
                            VidyoServiceClientBaseUrlRequestEid)
                    });
                    _statisticsWrapper = new StatisticsWrapper(_session);

                    // Spread the word
                    RaiseSessionConnectionUp();
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in SessionOnConnectionStateChanged: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void OnCustomNotificationReceived(object sender, CustomNotificationReceivedEventArgs e)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // New vidyo conversation request
                    if (e.Message.EventId.ToLower().Equals(VidyoNewConversationRequestEid.ToLower()) &&
                        e.Message.ObjectId.ToLower().Equals(VidyoNewConversationRequestOid.ToLower()))
                    {
                        var data = e.Message.ReadStrings();
                        if (data.Length == 1)
                            RaiseVidyoNewConversationRequested(data[0]);
                        else if (data.Length == 2)
                            RaiseVidyoNewConversationRequested(data[0], data[1]);
                        else
                            throw new Exception("Invalid data members! Expected 1 or 2 string parameters, got " + data.Length);

                        return;
                    }

                    // Request to get base URL
                    if (e.Message.EventId.ToLower().Equals(VidyoServiceClientBaseUrlRequestEid.ToLower()) &&
                        e.Message.ObjectId.ToLower().Equals(VidyoServiceClientBaseUrlRequestOid.ToLower()))
                    {
                        var username = e.Message.ReadStrings();
                        if (username.Length != 1)
                            throw new Exception("Unable to send URL without username!");

                        SendApplicationResponse(username[0], VidyoServiceClientBaseUrlResponseEid,
                            ConfigurationProperties.VidyoServiceEndpointUri);
                    }

                    Trace.Cic.warning("Unknown notification received! OID={}, EID={}",
                        e.Message.ObjectId, e.Message.EventId);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in OnCustomNotificationReceived: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void GenericInteractionOnDeallocated(object sender, EventArgs e)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = sender as Interaction;
                    if (interaction == null)
                        throw new ArgumentException("Interaction was null after casting sender to Interaction!",
                            "sender");

                    // Remove from main list of interactions
                    var listItem = _interactions.FirstOrDefault(i => i.InteractionId.Id == interaction.InteractionId.Id);
                    if (listItem != null)
                        _interactions.Remove(listItem);

                    // Return if this interaction isn't in the assignments list (it shouldn't be as long as we got a disconnect event first)
                    if (!_interactionAssignments.ContainsKey(interaction.InteractionId.Id)) return;

                    // Raise disconnected event
                    Trace.Cic.verbose("Interaction was deallocated and is still in list, raising disconnected event...");
                    _interactionAssignments.Remove(interaction.InteractionId.Id);
                    RaiseInteractionDisconnected(interaction);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GenericInteractionOnDeallocated: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void GenericInteractionOnAttributesChanged(object sender, AttributesEventArgs e)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Get interaction
                    var interaction = sender as Interaction;
                    if (interaction == null)
                        throw new ArgumentException("Interaction was null after casting sender to Interaction!",
                            "sender");

                    // Get assigned user
                    var assignedUser = interaction.UserQueueNames.Count > 0 ? interaction.UserQueueNames[0] : "";

                    // Get state
                    var state = interaction.State;

                    Trace.Cic.note("Evaluating interaction \"{}\"\nState: {}\nUser: {}\nChanges: {}",
                        interaction.InteractionId.Id, state, assignedUser,
                        e.InteractionAttributeNames.Aggregate((a, b) => a + ", " + b));

                    // Unassigned (likely queued to a workgroup)
                    if (string.IsNullOrEmpty(assignedUser) && !interaction.IsDisconnected &&
                        _interactionAssignments.ContainsKey(interaction.InteractionId.Id))
                    {
                        // Clear assignment
                        _interactionAssignments[interaction.InteractionId.Id] = "";
                    }

                    // Process assignment logic if state or user queue changed
                    if (e.InteractionAttributeNames.Contains(InteractionAttributeName.State) ||
                         e.InteractionAttributeNames.Contains(InteractionAttributeName.UserQueueNames))
                    {
                        // Assigned and connected
                        if (!string.IsNullOrEmpty(assignedUser) && interaction.IsConnected)
                        {
                            // If we don't have record of this assignment, invoke assignment logic
                            if (!_interactionAssignments.ContainsKey(interaction.InteractionId.Id))
                            {
                                // Add interaction to list
                                _interactionAssignments.Add(interaction.InteractionId.Id, assignedUser);

                                // Notify about assignment
                                RaiseInteractionAssigned(interaction, assignedUser);
                            }
                                // If it has been assigned to a new user, invoke assignment logic
                            else if (!assignedUser.Equals(_interactionAssignments[interaction.InteractionId.Id]))
                            {
                                // Set new username
                                _interactionAssignments[interaction.InteractionId.Id] = assignedUser;

                                // Notify about assignment
                                RaiseInteractionAssigned(interaction, assignedUser);
                            }
                            else
                                Trace.Cic.verbose("New assignment not detected.");
                        }

                        // Disconnected
                        if (e.InteractionAttributeNames.Contains(InteractionAttributeName.State) && 
                            interaction.IsDisconnected)
                        {
                            Trace.Cic.verbose("State changed and is disconnected, raising event...");

                            // Remove assignment
                            if (_interactionAssignments.ContainsKey(interaction.InteractionId.Id))
                                _interactionAssignments.Remove(interaction.InteractionId.Id);

                            // Raise disconnected event
                            RaiseInteractionDisconnected(interaction);
                        }
                    }

                    // Raise interaction changed event
                    RaiseInteractionChanged(interaction, interaction.GetStringAttributes(e.InteractionAttributeNames.ToArray()));

                    // Raise queue changed event
                    if (e.InteractionAttributeNames.Contains(InteractionAttributeName.UserQueueNames) ||
                        e.InteractionAttributeNames.Contains(InteractionAttributeName.WorkgroupQueueName) ||
                        e.InteractionAttributeNames.Contains(InteractionAttributeName.State)) // State will cause this to get re-triggered when a user picks up the interaction. Need this because it's added to the user's queue and picked up in different events.
                    {
                        if (interaction.UserQueueNames.Count > 0 && !string.IsNullOrEmpty(interaction.UserQueueNames[0]))
                            RaiseInteractionQueueChanged(interaction,
                                new QueueId(QueueType.User, interaction.UserQueueNames[0]));
                        else if (!string.IsNullOrEmpty(interaction.WorkgroupQueueName))
                            RaiseInteractionQueueChanged(interaction,
                                new QueueId(QueueType.Workgroup, interaction.WorkgroupQueueName));
                        else
                            Trace.Cic.warning("Interaction {} is not on a user or workgroup queue!", interaction.InteractionId.Id);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GenericInteractionOnAttributesChanged: " + ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion



        #region Private Methods

        private void RaiseInteractionAssigned(Interaction interaction, string agentName)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (InteractionAssigned != null)
                        InteractionAssigned(interaction.InteractionId.Id, agentName);
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseInteractionDisconnected(Interaction interaction)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (InteractionDisconnected != null)
                        InteractionDisconnected(interaction.InteractionId.Id);
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseInteractionChanged(Interaction interaction, IDictionary<string, string> attributes)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (InteractionChanged != null)
                        InteractionChanged(interaction.InteractionId.Id, attributes);
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseInteractionChangedNoWatch(Interaction interaction, IDictionary<string, string> attributes)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Only raise for non-watched attributes
                    var watchedAttrs = WatchedAttributes.Select(x => x.ToLower());
                    var dictionary =
                        attributes.Where(attr => !watchedAttrs.Contains(attr.Key.ToLower()))
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    RaiseInteractionChanged(interaction, dictionary);
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseInteractionQueueChanged(Interaction interaction, QueueId newQueue)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (InteractionQueueChanged != null)
                        InteractionQueueChanged(interaction.InteractionId.Id, newQueue.ScopedName,
                            newQueue.QueueType == QueueType.User && interaction.State == InteractionState.Connected
                                ? newQueue.QueueName
                                : "");
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseSessionConnectionLost()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (SessionConnectionLost != null)
                        SessionConnectionLost();
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseSessionConnectionUp()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (SessionConnectionUp != null)
                        SessionConnectionUp();
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private void RaiseVidyoNewConversationRequested(string username, string interactionId = "")
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (VidyoNewConversationRequested != null)
                        VidyoNewConversationRequested(username, interactionId);
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        private Interaction GetInteraction(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Find interaction
                    var interaction = _interactions.FirstOrDefault(i => i.InteractionId.Id == interactionId);

                    // Return if we have a cached object
                    if (interaction != null) return interaction;

                    // Try to find the interaction on the system (when the service is restarted, it won't have references)
                    interaction =
                        InteractionsManager.GetInstance(_session).CreateInteraction(new InteractionId(interactionId));

                    // Initialize interaction
                    if (interaction != null) InitializeInteraction(interaction, new string[] {});

                    return interaction;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                    //throw;
                    return null;
                }
            }
        }

        private void InitializeInteraction(Interaction interaction, string[] attributesToWatch)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Make list to watch
                    //var newAttributesToWatch = new string[WatchedAttributes.Length + attributesToWatch.Length];
                    //WatchedAttributes.CopyTo(newAttributesToWatch,0);
                    //attributesToWatch.CopyTo(newAttributesToWatch, WatchedAttributes.Length);
                    var newAttributesToWatch = new List<string>();
                    newAttributesToWatch.AddRange(WatchedAttributes);
                    newAttributesToWatch.AddRange(attributesToWatch);

                    if (interaction is CallbackInteraction)
                    {
                        //new[]
                        //{
                        //    CallbackInteractionAttributeName.CallbackMessage,
                        //    CallbackInteractionAttributeName.CallbackPhone
                        //}.CopyTo(newAttributesToWatch,
                        //    newAttributesToWatch.Length);

                        //var attrList = newAttributesToWatch.ToList();
                        newAttributesToWatch.Add(CallbackInteractionAttributeName.CallbackMessage);
                        newAttributesToWatch.Add(CallbackInteractionAttributeName.CallbackPhone);
                        //newAttributesToWatch = attrList.ToArray();
                    }

                    // Add interaction to list
                    if (!_interactions.Contains(interaction)) _interactions.Add(interaction);

                    // Start watching
                    var newAttrs = newAttributesToWatch.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
                    if (interaction.IsWatching())
                        interaction.ChangeWatchedAttributes(newAttrs, new string[] {}, false);
                    else
                        interaction.StartWatching(newAttrs);

                    // Add or remove from assigned list
                    if (interaction.UserQueueNames.Count > 0 && interaction.IsConnected)
                    {
                        if (!_interactionAssignments.ContainsKey(interaction.InteractionId.Id))
                            _interactionAssignments.Add(interaction.InteractionId.Id, interaction.UserQueueNames[0]);
                        else
                            _interactionAssignments[interaction.InteractionId.Id] = interaction.UserQueueNames[0];
                    }
                    else
                    {
                        // Remove from list
                        if (_interactionAssignments.ContainsKey(interaction.InteractionId.Id))
                            _interactionAssignments.Remove(interaction.InteractionId.Id);
                    }

                    // Register for changes
                    interaction.AttributesChanged += GenericInteractionOnAttributesChanged;
                    interaction.Deallocated += GenericInteractionOnDeallocated;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex, "Failed to create generic interaction! Error: {}", ex.Message);
                }
            }
        }

        private GenericInteraction DoMakeGenericInteraction(QueueId queue, GenericInteractionInitialState initialState,
            IEnumerable<KeyValuePair<string, string>> additionalAttributes)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Store in list to prevent multiple enumeration of IEnumerable
                    var additionalAttributesList = additionalAttributes == null
                        ? new List<KeyValuePair<string, string>>()
                        : additionalAttributes.ToList();

                    // Create parameters
                    var giParams = new GenericInteractionParameters(queue,
                        (InteractionState)Enum.Parse(typeof(InteractionState), initialState.ToString()));

                    // Add additional attributes
                    var newWatchedAttributes = new List<string>(WatchedAttributes);
                    foreach (var kvp in additionalAttributesList)
                    {
                        // Add as additional attribute if it's not a protected CIC attribute
                        if (!_cicProtectedAttributes.Any(s => s.ToLower().Equals(kvp.Key.ToLower())))
                            giParams.AdditionalAttributes.Add(kvp);

                        // Add to our list of things we're going to watch
                        newWatchedAttributes.Add(kvp.Key);
                    }

                    // Make GI
                    var interaction = InteractionsManager.GetInstance(_session).MakeGenericInteraction(giParams);

                    // Set up stuff
                    InitializeInteraction(interaction, additionalAttributesList.Select(x => x.Key).ToArray());

                    Console.WriteLine("MakeGenericInteraction -- Id:{0} - Queue: {1}",
                        interaction.InteractionId.Id, interaction.WorkgroupQueueName);

                    // Return
                    return interaction;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex, "Failed to create generic interaction! Error: {}", ex.Message);
                    return null;
                }
            }
        }

        private CallbackInteraction DoMakeCallbackInteraction(QueueId queue, string callbackPhone, string callbackMessage,
            IEnumerable<KeyValuePair<string, string>> additionalAttributes)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Store in list to prevent multiple enumeration of IEnumerable
                    var additionalAttributesList = additionalAttributes == null
                        ? new List<KeyValuePair<string, string>>()
                        : additionalAttributes.ToList();

                    // Create parameters
                    var cbParams = new CallbackInteractionParameters(queue.ScopedName, callbackPhone, callbackMessage, new Dictionary<string, string>());

                    // Add additional attributes
                    var newWatchedAttributes = new List<string>(WatchedAttributes);
                    foreach (var kvp in additionalAttributesList)
                    {
                        // Add as additional attribute if it's not a protected CIC attribute
                        if (!_cicProtectedAttributes.Any(s => s.ToLower().Equals(kvp.Key.ToLower())))
                            cbParams.AdditionalAttributes.Add(kvp.Key, kvp.Value);

                        // Add to our list of things we're going to watch
                        newWatchedAttributes.Add(kvp.Key);
                    }

                    // Make callback
                    var interaction = InteractionsManager.GetInstance(_session).MakeCallbackInteraction(cbParams);

                    // Set up stuff
                    InitializeInteraction(interaction, additionalAttributesList.Select(x => x.Key).ToArray());

                    return interaction;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex, "Failed to create callback interaction! Error: {}", ex.Message);
                    return null;
                }
            }
        }

        private Interaction DoMakeInteraction(VideoConversationInitializationParameters parameters)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    switch (parameters.MediaType)
                    {
                        case VideoConversationMediaType.GenericInteraction:
                            {
                                // Cast parameters
                                var giParameters =
                                    parameters as GenericInteractionVideoConversationInitializationParameters;

                                // Create interaction
                                return DoMakeGenericInteraction(new QueueId(giParameters.ScopedQueueName),
                                    giParameters.InitialState, parameters.AdditionalAttributes);
                            }
                        case VideoConversationMediaType.Callback:
                            {
                                // Cast parameters
                                var callbackParameters = parameters as CallbackVideoConversationInitializationParameters;

                                // Create interaction
                                return DoMakeCallbackInteraction(new QueueId(callbackParameters.ScopedQueueName),
                                    callbackParameters.CallbackPhoneNumber, callbackParameters.CallbackMessage,
                                    parameters.AdditionalAttributes);
                            }
                        default:
                            throw new Exception("Unable to make interaction for media type: " + parameters.MediaType);
                    }
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex, "Failed to create callback interaction! Error: {}", ex.Message);
                    return null;
                }
            }
        }

        #endregion



        #region Public Methods

        public void Connect()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Return if already connected
                    if (IsConnected) return;

                    // Connect
                    if (ConfigurationProperties.CicUseWindowsAuth)
                        ConnectWithWindowsAuth(ConfigurationProperties.CicServer);
                    else
                        Connect(ConfigurationProperties.CicServer,
                            ConfigurationProperties.CicUsername,
                            ConfigurationProperties.CicPassword);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in Connect: " + ex.Message, EventId.CICConnectionLogOnFailed);
                    throw;
                }
            }
        }

        public void Connect(string server, string user, string password)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    _session.Connect(new SessionSettings(),
                        new HostSettings(new HostEndpoint(server)),
                        new ICAuthSettings(user, password),
                        new StationlessSettings());
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in Connect: " + ex.Message, EventId.CICConnectionLogOnFailed);
                    throw;
                }
            }
        }

        public void ConnectWithWindowsAuth(string server)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    _session.Connect(new SessionSettings(),
                        new HostSettings(new HostEndpoint(server)),
                        new WindowsAuthSettings(), 
                        new StationlessSettings());
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in ConnectWithWindowsAuth: " + ex.Message, EventId.CICConnectionLogOnFailed);
                    throw;
                }
            }
        }

        public void SendApplicationResponse(string oid, string eid, params string[] data)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var response =
                        new CustomResponse(
                            new CustomMessage(new CustomMessageHeader(CustomMessageType.ApplicationResponse, oid, eid)));
                    response.Write(data);
                    _customNotification.SendApplicationResponse(response);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in SendApplicationResponse: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public string GetAttribute(long interactionId, string attributeName)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var result = GetAttributes(interactionId, new[] {attributeName});
                    return result.Count == 0 ? "" : result[attributeName];
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetAttribute: " + ex.Message, EventId.GenericError);
                    return "";
                }
            }
        }

        public IDictionary<string, string> GetAttributes(long interactionId, string[] attributeNames)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    return GetInteraction(interactionId).GetStringAttributes(attributeNames);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetAttributes: " + ex.Message, EventId.GenericError);
                    return new Dictionary<string, string>();
                }
            }
        }

        public void SetAttribute(long interactionId, string attributeName, string attributeValue)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    SetAttributes(interactionId, new Dictionary<string, string> {{attributeName, attributeValue}});
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in SetAttribute: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public void SetAttributes(long interactionId, Dictionary<string,string> attributes)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    GetInteraction(interactionId).SetStringAttributes(attributes);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in SetAttribute: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public bool IsInteractionAssigned(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    if (interaction == null)
                    {
                        Trace.Cic.warning("No interaction with ID {}", interactionId);
                        return false;
                    }
                    return interaction.UserQueueNames.Count > 0 && !string.IsNullOrEmpty(interaction.UserQueueNames[0]);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in IsInteractionAssigned: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public QueueInfo GetQueueInfo(string queueName, bool waitForData)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    return new QueueInfo
                    {
                        QueueName = queueName,
                        NumberAvailableForAcdInteractions =
                            _statisticsWrapper.GetNumberAvailableForAcdInteractions(queueName, waitForData),
                        AverageWaitTimeCurrentPeriod = _statisticsWrapper.GetAverageWaitTime(queueName, IntervalTypes.CurrentPeriod, waitForData),
                        AverageWaitTimePreviousPeriod = _statisticsWrapper.GetAverageWaitTime(queueName, IntervalTypes.PreviousPeriod, waitForData),
                        AverageWaitTimeCurrentShift = _statisticsWrapper.GetAverageWaitTime(queueName, IntervalTypes.CurrentShift, waitForData),
                        AverageWaitTimePreviousShift = _statisticsWrapper.GetAverageWaitTime(queueName, IntervalTypes.PreviousShift, waitForData),
                        InteractionCount = _statisticsWrapper.GetInteractionCount(queueName, waitForData),
                        InteractionsWaiting = _statisticsWrapper.GetInteractionsWaiting(queueName, waitForData),
                        PercentAvailable = _statisticsWrapper.GetPercentAvailable(queueName, waitForData)
                    };
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetQueueInfo: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public long MakeInteraction(VideoConversationInitializationParameters parameters)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    return DoMakeInteraction(parameters).InteractionId.Id;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in MakeInteraction: " + ex.Message, EventId.GenericError);
                    return 0;
                }
            }
        }

        public bool InteractionExists(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    return GetInteraction(interactionId) != null;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in InteractionExists: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public bool InteractionIsConnected(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    return interaction.IsConnected;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in InteractionIsConnected: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public bool InteractionIsDisconnected(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    return interaction.IsDisconnected;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in InteractionIsDisconnected: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public bool InteractionIsHeld(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    return interaction.IsHeld;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in InteractionIsHeld: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public string GetWorkgroupQueueName(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    return interaction.WorkgroupQueueName;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetWorkgroupQueueName: " + ex.Message, EventId.GenericError);
                    return string.Empty;
                }
            }
        }

        public string GetUserQueueName(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    return interaction.UserQueueNames.Count > 0 ? interaction.UserQueueNames[0] : string.Empty;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetUserQueueName: " + ex.Message, EventId.GenericError);
                    return string.Empty;
                }
            }
        }

        public VideoConversationMediaType GetInteractionType(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var interaction = GetInteraction(interactionId);
                    switch (interaction.InteractionType)
                    {
                        case InteractionType.None:
                            return VideoConversationMediaType.None;
                        case InteractionType.Callback:
                            return VideoConversationMediaType.Callback;
                        case InteractionType.Chat:
                            return VideoConversationMediaType.Chat;
                        case InteractionType.Generic:
                            return VideoConversationMediaType.GenericInteraction;
                        default:
                            return VideoConversationMediaType.Other;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetInteractionMediaType: " + ex.Message, EventId.GenericError);
                    return VideoConversationMediaType.None;
                }
            }
        }

        public VideoConversationInitializationParameters GetParametersFromInteraction(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Get interaction (it will be auto-casted to the correct type)
                    return GetInteraction(interactionId);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GetInteractionMediaType: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public void SendChatText(long interactionId, string message)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Get chat
                    var chat = GetInteraction(interactionId) as ChatInteraction;
                    if (chat == null)
                    {
                        Trace.Cic.warning("Cannot send message to {} because it is not a chat!", interactionId);
                        return;
                    }

                    // Send message
                    chat.SendText(message);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in SendChatText: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public void SendChatUrl(long interactionId, Uri uri)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Get chat
                    var chat = GetInteraction(interactionId) as ChatInteraction;
                    if (chat == null)
                    {
                        Trace.Cic.warning("Cannot send URI to {} because it is not a chat!", interactionId);
                        return;
                    }

                    // Send message
                    chat.SendUrl(uri);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in SendChatUrl: " + ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion
    }
}
