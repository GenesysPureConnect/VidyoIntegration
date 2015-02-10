using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Forms;
using AutoCompleteTextBoxLib;
using ININ.IceLib.Connection;
using ININ.IceLib.Connection.Extensions;
using ININ.IceLib.Interactions;
using ININ.IceLib.People;
using ININ.InteractionClient.AddIn;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.VidyoTypes;
using VidyoIntegration.VidyoAddin.ViewModel.Helpers;
using InteractionEventArgs = ININ.IceLib.Interactions.InteractionEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace VidyoIntegration.VidyoAddin.ViewModel
{
    public class VidyoPanelViewModel : ViewModelBase
    {
        #region Private Vars

        private Session _session;
        private CustomNotification _customNotification;

        private static VidyoPanelViewModel _instance;
        private bool _isInitialized = false;
        private readonly object _screenPopLocker = new object();
        readonly BackgroundWorker _rewatchWorker = new BackgroundWorker();
        private InteractionViewModel _selectedInteraction;
        private IInteractionSelector _interactionSelector;
        private const string VidyoNewConversationRequestOid = "VidyoNewConversationRequest";
        private const string VidyoNewConversationRequestEid = "VidyoNewConversationRequest";
        private const string JoinVidyoConferenceRequestEid = "JoinVidyoConferenceRequest";
        private const string JoinVidyoConferenceResponseEid = "JoinVidyoConferenceResponse";
        private const string VidyoServiceClientBaseUrlRequestOid = "VidyoServiceClientBaseUrlRequest";
        private const string VidyoServiceClientBaseUrlRequestEid = "VidyoServiceClientBaseUrlRequest";
        private const string VidyoServiceClientBaseUrlResponseEid = "VidyoServiceClientBaseUrlResponse";

        private readonly List<string> _watchedAttrs = new List<string>
        {
            InteractionAttributeName.InteractionId,
            InteractionAttributeName.InteractionType,
            InteractionAttributeName.RemoteName,
            InteractionAttributeName.State,
            InteractionAttributeName.StateDescription,
            InteractionAttributeName.UserQueueNames,
            InteractionAttributeName.WorkgroupQueueName,
            VideoIntegrationAttributeNames.VideoConversationId,
            VideoIntegrationAttributeNames.VideoLastAgentToScreenPop,
            VideoIntegrationAttributeNames.VideoRoomId,
            VideoIntegrationAttributeNames.VideoRoomUrl
        };

        private ObservableCollection<InteractionViewModel> _interactions;

        #endregion



        #region Public Properties

        public static VidyoPanelViewModel Instance
        {
            get { return _instance ?? (_instance = new VidyoPanelViewModel()); }
        }

        public InteractionQueue MyInteractions { get; private set; }

        public bool IsInitialized
        {
            get { return _isInitialized; }
            set
            {
                _isInitialized = value; 
                OnPropertyChanged();
            }
        }

        public ObservableCollection<InteractionViewModel> Interactions
        {
            get { return _interactions; }
            set
            {
                _interactions = value; 
                OnPropertyChanged();
            }
        }

        public InteractionViewModel SelectedInteraction
        {
            get { return _selectedInteraction; }
            set
            {
                _selectedInteraction = value; 
                OnPropertyChanged();
            }
        }

        #endregion



        #region Constructor

        public VidyoPanelViewModel()
        {
            using (Trace.Main.scope())
            {
                Interactions = new ObservableCollection<InteractionViewModel>();
                _rewatchWorker.DoWork += RewatchWorkerOnDoWork;
            }
        }

        #endregion



        #region Private Methods

        private void SessionOnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    if (e.State == ConnectionState.Up)
                    {
                        Trace.Main.always("Reinitializing!");
                        Initialize(_session, _interactionSelector);
                    }

                    // Clean up
                    if (e.State != ConnectionState.Up)
                    {
                        // Because Icelib cannot be trusted not to throw bizarre exceptions, supress errors from stopwatching
                        try
                        {
                            if (_customNotification != null && _customNotification.IsWatching())
                            {
                                _customNotification.StopWatching();
                                _customNotification.CustomNotificationReceived -= OnCustomNotificationReceived;
                                _customNotification = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.Main.warning("Supressing error encountered while stopping custom notification watch: {}", ex.Message);
                        }
                        try
                        {
                            if (MyInteractions != null && MyInteractions.IsWatching())
                            {
                                MyInteractions.StopWatching();
                                MyInteractions.InteractionAdded -= MyInteractionsOnInteractionAdded;
                                MyInteractions.InteractionChanged -= MyInteractionsOnInteractionChanged;
                                MyInteractions.InteractionRemoved -= MyInteractionsOnInteractionRemoved;
                                MyInteractions = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.Main.warning("Supressing error encountered while stopping my interactions watch: {}", ex.Message);
                        }

                        Context.Send(s =>
                        {
                            Interactions.Clear();
                            SelectedInteraction = null;
                        }, null);
                    }
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex);
                }
            }
        }

        private InteractionViewModel GetInteractionViewModel(Interaction interaction)
        {
            // Does this interaction have a video conversation attached?
            if (string.IsNullOrEmpty(interaction.GetStringAttribute(VideoIntegrationAttributeNames.VideoConversationId)))
            {
                Trace.Main.status("{} does not have a video conversation", interaction.InteractionId.Id);
                return null;
            }

            // Try to find an existing VM first
            var interactionVm = Interactions.FirstOrDefault(i => i.InteractionId.Equals(interaction.InteractionId.Id));
            if (interactionVm != null) return interactionVm;

            Context.Send(s =>
            {
                try
                {
                    // Create view model
                    interactionVm = InteractionViewModel.FromInteraction(interaction);

                    // Add to list
                    Interactions.Add(interactionVm);

                    // Set it as the selected interaction (forces the interaction's tab to show within the vidyo tab addin)
                    SelectedInteraction = interactionVm;
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex);
                }
            }, null);

            // Auto answer because of reconstitution?
            if (interactionVm.VidyoAutoAnswerOnReconstitution) interaction.Pickup();

            return interactionVm;
        }

        private void MyInteractionsOnInteractionAdded(object sender, InteractionAttributesEventArgs e)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    Context.Send(s =>
                    {
                        using (Trace.Main.scope("MyInteractionsOnInteractionAdded"))
                        {
                            try
                            {
                                var vm = GetInteractionViewModel(e.Interaction);
                                if (vm == null) return;

                                // Screen pop
                                TryScreenPop(e.Interaction);
                            }
                            catch (Exception ex)
                            {
                                Trace.Main.exception(ex);
                            }
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex);
                }
            }
        }

        private void MyInteractionsOnInteractionChanged(object sender, InteractionAttributesEventArgs e)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // Find the interaction VM
                    var interactionVm = GetInteractionViewModel(e.Interaction);
                    if (interactionVm == null)
                        return;
                    else
                        interactionVm.RaiseInteractionPropertyChanged(e.InteractionAttributeNames);

                    // Try screen pop
                    if (e.InteractionAttributeNames.Contains(VideoIntegrationAttributeNames.VideoRoomUrl) || // Due to attached convo
                        e.InteractionAttributeNames.Contains(InteractionAttributeName.State)) // Due to pickup
                    {
                        TryScreenPop(e.Interaction);
                    }
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        private void MyInteractionsOnInteractionRemoved(object sender, InteractionEventArgs e)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    Context.Send(s =>
                    {
                        using (Trace.Main.scope("MyInteractionsOnInteractionRemoved"))
                        {
                            try
                            {
                                // Remove from interactions list
                                var interaction = GetInteractionViewModel(e.Interaction);
                                if (interaction != null)
                                    Interactions.Remove(interaction);
                            }
                            catch (Exception ex)
                            {
                                Trace.Main.exception(ex, ex.Message);
                            }
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        private void OnCustomNotificationReceived(object sender, CustomNotificationReceivedEventArgs e)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // This is a request for this agent to join a Vidyo conversation
                    if (e.Message.ObjectId.Equals(_session.UserId, StringComparison.InvariantCultureIgnoreCase) &&
                        e.Message.EventId.ToLower().Equals(JoinVidyoConferenceRequestEid, StringComparison.InvariantCultureIgnoreCase))
                    {
                        HandleJoinVidyoConferenceRequest(e);
                        return;
                    }

                    // This is a response for this agent to join a Vidyo conversation
                    if (e.Message.ObjectId.Equals(_session.UserId, StringComparison.InvariantCultureIgnoreCase) &&
                        e.Message.EventId.ToLower().Equals(JoinVidyoConferenceResponseEid, StringComparison.InvariantCultureIgnoreCase))
                    {
                        HandleJoinVidyoConferenceResponse(e);
                        return;
                    }

                    // This is a response with the vidyo base URL
                    if (e.Message.ObjectId.Equals(_session.UserId, StringComparison.InvariantCultureIgnoreCase) &&
                        e.Message.EventId.ToLower().Equals(VidyoServiceClientBaseUrlResponseEid, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var data = e.Message.ReadStrings();
                        if (data.Length != 1)
                            throw new Exception("Did not get correct data back from VidyoServiceClientBaseUrlResponseEid request!");
                        VidyoServiceClient.BaseUrl = data[0];
                        return;
                    }

                    // If we got here, bad news.
                    Trace.Main.warning("Unexpected notification received! OID: " + e.Message.ObjectId +
                                       ", EID: " + e.Message.EventId);
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex);
                }
            }
        }

        private void HandleJoinVidyoConferenceRequest(CustomNotificationReceivedEventArgs e)
        {
            /* Get data
             * [0] = Requesting user
             * [1] = Message from user
             * [2] = Join url (guest link)
             */
            var data = e.Message.ReadStrings();
            if (data.Length != 3)
                throw new Exception("Invalid data members! Expected 3 string parameters, got " + data.Length);
            var requestingUsername = data[0];
            var requestMessage = data[1];
            var joinUrl = data[2];

            var result =
                MessageBox.Show(requestingUsername + " has invited you to join a video conference." +
                                Environment.NewLine +
                                "Message: " + requestMessage +
                                Environment.NewLine + Environment.NewLine +
                                "Do you wish to join this conference?",
                    "Video conference invitation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

            if (result != MessageBoxResult.Yes)
            {
                // Send decline message
                SendCustomNotification(CustomMessageType.ApplicationResponse, requestingUsername,
                    JoinVidyoConferenceResponseEid, _session.UserId, "Request rejected");
                return;
            }

            // Launch URL for user
            Process.Start(MakeRoomUrl(joinUrl, FormatAgentName(_session.UserId)));
        }

        private void HandleJoinVidyoConferenceResponse(CustomNotificationReceivedEventArgs e)
        {
            /* Get data
             * [0] = Responding user
             * [1] = Message from user 
             */
            var data = e.Message.ReadStrings();
            if (data.Length != 2)
                throw new Exception("Invalid data members! Expected 2 string parameters, got " + data.Length);
            var respondingUsername = data[0];
            var requestMessage = data[1];

            MessageBox.Show(respondingUsername + " has declined your invitation." +
                            Environment.NewLine +
                            "Message: " + requestMessage,
                "Video conference invitation rejected",
                MessageBoxButton.OK,
                MessageBoxImage.Exclamation,
                MessageBoxResult.OK);
        }

        private void TryScreenPop(Interaction interaction)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // Lock to prevent any concurrency issues
                    lock (_screenPopLocker)
                    {
                        // Check reasons to quit
                        if (interaction.IsDisconnected || // Disconnected
                            !interaction.IsConnected || // Not connected
                            HasUrlAlreadyPopped(interaction)) // Already popped
                            //!string.IsNullOrEmpty(interactionVm.VidyoRoomUrl)) // No url set
                            return;

                        // Find the interaction VM
                        var interactionVm =
                            Interactions.FirstOrDefault(i => i.InteractionId.Equals(interaction.InteractionId.Id));
                        if (interactionVm == null)
                            throw new Exception("Failed to find interaction view model for interaction id " +
                                                interaction.InteractionId);

                        // Pop the URL
                        Trace.Main.note("Popping agent Vidyo URL");
                        Process.Start(MakeRoomUrl(interactionVm.VidyoRoomUrl, FormatAgentName(_session.UserId)));

                        // Set the user's name so we know that this has been popped
                        interaction.SetStringAttribute(VideoIntegrationAttributeNames.VideoLastAgentToScreenPop, _session.UserId);
                    }
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message, "MyInteractionsOnInteractionChanged");
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        private string MakeRoomUrl(string roomUrl, string guestName)
        {
            return roomUrl + "&guestName=" + HttpUtility.UrlEncode(guestName);
        }

        private string FormatAgentName(string agentName)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var part in agentName.Split('.'))
                {
                    sb.Append(SplitUpperCase(part).Trim(' ') + " ");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return agentName;
            }
        }

        private string SplitUpperCase(string s)
        {
            var r = new Regex(@"
                (?<=[A-Z])(?=[A-Z][a-z]) |
                 (?<=[^A-Z])(?=[A-Z]) |
                 (?<=[A-Za-z])(?=[^A-Za-z])", RegexOptions.IgnorePatternWhitespace);

            return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(r.Replace(s, "_"));
        }

        private void RewatchWorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // Sleeping to avoid a race condition with the watch auto-stopping
                    Thread.Sleep(800);
                    WatchCustomNotifications();
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        private void WatchCustomNotifications()
        {
            if (!_customNotification.IsWatching())
                _customNotification.StopWatching();

            _customNotification.StartWatching(new[]
            {
                new CustomMessageHeader(
                    CustomMessageType.ApplicationRequest,
                    _session.UserId.ToLower(),
                    JoinVidyoConferenceRequestEid),
                new CustomMessageHeader(
                    CustomMessageType.ApplicationResponse,
                    _session.UserId.ToLower(),
                    JoinVidyoConferenceResponseEid),
                new CustomMessageHeader(
                    CustomMessageType.ApplicationResponse,
                    _session.UserId.ToLower(),
                    VidyoServiceClientBaseUrlResponseEid)
            });
        }

        private bool HasUrlAlreadyPopped(Interaction interaction)
        {
            return
                interaction.GetWatchedStringAttribute(VideoIntegrationAttributeNames.VideoLastAgentToScreenPop)
                    .Equals(_session.UserId, StringComparison.InvariantCultureIgnoreCase);
        }

        private void FilterForTransfer(object data)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // Cast args
                    var args = (FilterTextChangedEventArgs) data;

                    // Get entries from server
                    var entries = PeopleManager.GetInstance(_session).GetLookupEntries(new LookupParameters
                    {
                        LookupString = args.Text,
                        ColumnsToMatch =
                        {
                            LookupEntryProperty.Extension,
                            LookupEntryProperty.FirstName,
                            LookupEntryProperty.LastName,
                            LookupEntryProperty.DisplayName,
                            LookupEntryProperty.EntryId
                        },
                        ComparisonType = LookupComparisonType.Contains,
                        DirectoriesToSearch =
                        {
                            LookupEntryType.User,
                            LookupEntryType.Workgroup
                        },
                        IncludeUsersExcludedFromCompanyDirectory = true,
                        MaxEntries = 30
                    });

                    // Complete the filter
                    args.Source.CompleteDeferredFilter(entries.LookupEntries.Select(entry=>(LookupEntryViewModel)entry), args.EventId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        private void SendCustomNotification(CustomMessageType messageType, string objectId, string eventId,
            params string[] data)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    Trace.Main.note("SendCustomNotification request\nCustomMessageType: {}\nOID: {}\nEID: {}\nData:\n{}", messageType, objectId, eventId,
                        data == null ? "*NO DATA*" : data.Aggregate((a, b) => a + "\n" + b));

                    switch (messageType)
                    {
                        case CustomMessageType.ServerNotification:
                        {
                            var requestServerNotification =
                                new CustomRequest(new CustomMessageHeader(messageType, objectId, eventId));
                            if (data != null && data.Length > 0) requestServerNotification.Write(data);
                            _customNotification.SendServerRequestNoResponse(requestServerNotification);
                            break;
                        }
                        case CustomMessageType.ApplicationRequest:
                        {
                            var requestApplicationRequest =
                                new CustomRequest(new CustomMessageHeader(messageType, objectId, eventId));
                            if (data != null && data.Length > 0) requestApplicationRequest.Write(data);
                            _customNotification.SendApplicationRequest(requestApplicationRequest);
                            break;
                        }
                        case CustomMessageType.ApplicationResponse:
                        {
                            var responseApplicationResponse =
                                new CustomResponse(
                                    new CustomMessage(new CustomMessageHeader(messageType, objectId, eventId)));
                            if (data != null && data.Length > 0) responseApplicationResponse.Write(data);
                            _customNotification.SendApplicationResponse(responseApplicationResponse);
                            break;
                        }
                        default:
                            throw new Exception("Unsupported message type: " + messageType);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex);
                }
            }
        }

        #endregion



        #region Public Methods

        public void Initialize(Session session, IInteractionSelector interactionSelector)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    Trace.Main.always("Initializing VidyoPanelViewModel");

                    // Set things
                    if (_session == null)
                    {
                        // Only do these things the first time around
                        _session = session;
                        _session.ConnectionStateChanged += SessionOnConnectionStateChanged;
                    }

                    _interactionSelector = interactionSelector;

                    _customNotification = new CustomNotification(_session);
                    MyInteractions = new InteractionQueue(InteractionsManager.GetInstance(_session),
                        new QueueId(QueueType.User, _session.UserId));

                    // Watch queue
                    MyInteractions.InteractionAdded += MyInteractionsOnInteractionAdded;
                    MyInteractions.InteractionChanged += MyInteractionsOnInteractionChanged;
                    MyInteractions.InteractionRemoved += MyInteractionsOnInteractionRemoved;
                    MyInteractions.StartWatching(_watchedAttrs.ToArray());

                    // Watch for custom notifications
                    _customNotification.CustomNotificationReceived += OnCustomNotificationReceived;
                    WatchCustomNotifications();

                    // Get the Vidyo service client base URL
                    SendCustomNotification(CustomMessageType.ApplicationRequest, VidyoServiceClientBaseUrlRequestOid,
                        VidyoServiceClientBaseUrlRequestEid, _session.UserId.ToLower());

                    // Let everyone know we're ready
                    IsInitialized = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                    //MessageBox.Show(
                    //    "There was an error intializing the Vidyo addin. Please contact your system administrator.",
                    //    "Vidyo Addin - critical error");
                    throw;
                }
            }
        }

        public void StartVidyoChatForAgent()
        {
            using (Trace.Main.scope())
            {
                try
                {
                    Trace.Main.status("Requesting new vidyo interaction for user {}", _session.UserId);

                    /* True if:
                     *  - An interaction is selected
                     *  - It's a chat or callback
                     *  - There's not already a video conversation attached to the interaction
                     */
                    if (_interactionSelector.SelectedInteraction != null &&
                        (_interactionSelector.SelectedInteraction.GetAttribute(InteractionAttributeName.InteractionType)
                            .Equals(InteractionAttributeValues.InteractionType.Chat,
                                StringComparison.InvariantCultureIgnoreCase) ||
                         _interactionSelector.SelectedInteraction.GetAttribute(InteractionAttributeName.InteractionType)
                             .Equals(InteractionAttributeValues.InteractionType.Callback,
                                 StringComparison.InvariantCultureIgnoreCase)) &&
                        string.IsNullOrEmpty(
                            _interactionSelector.SelectedInteraction.GetAttribute(
                                VideoIntegrationAttributeNames.VideoConversationId)))
                    {
                        var interactionId =
                            _interactionSelector.SelectedInteraction.GetAttribute(InteractionAttributeName.InteractionId);

                        // Ask to attach, create new, or cancel
                        var result =
                            MessageBox.Show(
                                "Attach a new video chat for the selected interaction \"" +
                                _interactionSelector.SelectedInteraction.GetAttribute(
                                    InteractionAttributeName.RemoteName) + "\" (" + interactionId + ")?" +
                                Environment.NewLine + Environment.NewLine +
                                "[Yes] to attach a video chat to this interaction" + Environment.NewLine +
                                "[No] to start a new video interaction", "Attach or create a video conversation?",
                                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);

                        switch (result)
                        {
                            case MessageBoxResult.Yes:
                                // Send with interaction ID
                                SendCustomNotification(CustomMessageType.ApplicationRequest,
                                    VidyoNewConversationRequestOid,
                                    VidyoNewConversationRequestEid, _session.UserId, interactionId);
                                return;
                            case MessageBoxResult.No:
                                // Send without interaction ID
                                SendCustomNotification(CustomMessageType.ApplicationRequest,
                                    VidyoNewConversationRequestOid,
                                    VidyoNewConversationRequestEid, _session.UserId);
                                return;
                            default:
                                // Canceled
                                return;
                        }
                    }

                    // Ask to create or cancel
                    var mboxResult = MessageBox.Show("Create a new video conversation?",
                        "Create a new video conversation?", MessageBoxButton.YesNo, MessageBoxImage.Question,
                        MessageBoxResult.No);

                    if (mboxResult == MessageBoxResult.Yes)
                    {
                        // Send without interaction ID
                        SendCustomNotification(CustomMessageType.ApplicationRequest, VidyoNewConversationRequestOid,
                            VidyoNewConversationRequestEid, _session.UserId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void FilterForTransferAsync(FilterTextChangedEventArgs e)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // Start filter process async
                    var t = new Thread(FilterForTransfer);
                    t.Start(e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void InviteToConference(InteractionViewModel interaction, string message)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    if (interaction == null)
                        throw new ArgumentNullException("interaction");
                    if (interaction.TransferTarget == null)
                        throw new ArgumentNullException("interaction.TransferTarget");
                    if (interaction.TransferTarget.Entry.LookupEntryType != LookupEntryType.User)
                        throw new Exception("Only users can be invited to conferences! Invalid target: " +
                                            interaction.TransferTarget.Entry.LookupEntryType);

                    /* Invite target
                     * [0] = Requesting user
                     * [1] = Message from user
                     * [2] = Join url (guest link)
                     */
                    SendCustomNotification(CustomMessageType.ApplicationRequest, interaction.TransferTarget.Entry.EntryId,
                        JoinVidyoConferenceRequestEid, _session.UserId, message, "http://www.inin.com");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        #endregion
    }
}
