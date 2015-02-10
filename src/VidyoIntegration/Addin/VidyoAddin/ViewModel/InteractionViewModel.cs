using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Timers;
using ININ.IceLib.Interactions;
using ININ.IceLib.People;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.VidyoTypes;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using VidyoIntegration.VidyoAddin.ViewModel.Helpers;
using Timer = System.Timers.Timer;

namespace VidyoIntegration.VidyoAddin.ViewModel
{
    public class InteractionViewModel : ViewModelBase
    {
        #region Private Fields

        private Interaction _interaction = null;
        private LookupEntryViewModel _transferTarget;
        private Timer _participantCheckerTimer = new Timer(1000);

        private ParticipantCollection<Participant> _participants = new ParticipantCollection<Participant>();
        private bool _isCheckingParticipants;

        #endregion



        #region Public Properties

        public long InteractionId { get { return _interaction.InteractionId.Id; } }

        public InteractionType InteractionType { get { return _interaction.InteractionType; } }

        public string RemoteName { get { return _interaction.RemoteName; } }

        public InteractionState State { get { return _interaction.State; } }

        public string StateDescription { get { return _interaction.StateDescription; } }

        public string UserName
        {
            get { return _interaction.UserQueueNames.Count > 0 ? _interaction.UserQueueNames[0] : ""; }
        }

        public string WorkgroupQueueName { get { return _interaction.WorkgroupQueueName; } }

        public string VidyoConversationId
        {
            get
            {
                return _interaction.GetWatchedStringAttribute(VideoIntegrationAttributeNames.VideoConversationId);
            }
        }

        public int VidyoRoomId
        {
            get
            {
                return _interaction.GetWatchedIntegerAttribute(VideoIntegrationAttributeNames.VideoRoomId);
            }
        }

        public string VidyoRoomUrl
        {
            get
            {
                return _interaction.GetWatchedStringAttribute(VideoIntegrationAttributeNames.VideoRoomUrl);
            }
        }

        public bool VidyoAutoAnswerOnReconstitution
        {
            get
            {
                var x = false;
                bool.TryParse(
                    _interaction.GetStringAttribute(VideoIntegrationAttributeNames.VideoAutoAnswerOnReconstitution),
                    out x);
                return x;
            }
        }

        public LookupEntryViewModel TransferTarget
        {
            get { return _transferTarget; }
            set
            {
                Console.WriteLine("Transfer target = " + value);
                _transferTarget = value; 
                OnPropertyChanged();
                OnPropertyChanged("HasTransferTarget");
            }
        }

        public bool HasTransferTarget { get { return TransferTarget != null; } }

        public ParticipantCollection<Participant> Participants
        {
            get { return _participants; }
            set
            {
                _participants = value; 
                OnPropertyChanged();
            }
        }

        public bool IsCheckingParticipants
        {
            get { return _isCheckingParticipants; }
            set
            {
                _isCheckingParticipants = value; 
                OnPropertyChanged();
            }
        }

        #endregion



        #region Constructor

        private InteractionViewModel()
        {
            _participantCheckerTimer.AutoReset = true;
            _participantCheckerTimer.Elapsed += ParticipantCheckerTimerOnElapsed;
            _participantCheckerTimer.Start();
        }

        #endregion



        #region Private methods

        private void ParticipantCheckerTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    Context.Send(s => IsCheckingParticipants = true, null);

                    // Stop the timer so we can't back up on web service calls
                    _participantCheckerTimer.Stop();

                    //Console.WriteLine("Getting participants for room " + VidyoRoomId);

                    // Call web service to get participant list
                    var participants = VidyoServiceClient.GetParticipants(VidyoRoomId);
                    //Console.WriteLine("Participants (" + participants.Count + "): " + participants.Select(p => p.DisplayName).Aggregate((a, b) => a + "; " + b));

                    // Update list
                    Context.Send(s => Participants.AddRange(participants, true), null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error getting participants: " + ex.Message);
                    Trace.Main.exception(ex);
                }
                finally
                {
                    Context.Send(s => IsCheckingParticipants = false, null);

                    // Only restart the timer if the interaction isn't disconnected
                    if (!_interaction.IsDisconnected) _participantCheckerTimer.Start();
                }
            }
        }

        public void DoMuteAudio(object data)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    var parts = data as Tuple<Participant, bool>;
                    VidyoServiceClient.MuteAudio(VidyoRoomId, parts.Item1, parts.Item2);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void DoMuteVideo(object data)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    var parts = data as Tuple<Participant, bool>;
                    VidyoServiceClient.MuteVideo(VidyoRoomId, parts.Item1, parts.Item2);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void DoKickParticipant(object data)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    var participant = data as Participant;
                    VidyoServiceClient.KickParticipant(VidyoRoomId, participant);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        #endregion



        #region Public methods

        public static InteractionViewModel FromInteraction(Interaction interaction)
        {
            var i = new InteractionViewModel {_interaction = interaction};
            return i;
        }

        public void RaiseInteractionPropertyChanged(string propertyName)
        {
            RaiseInteractionPropertyChanged(new[] {propertyName});
        }

        public void RaiseInteractionPropertyChanged(IEnumerable<string> propertyNames)
        {
            Context.Send(s =>
            {
                using (Trace.Main.scope("RaiseIceLibPropertyChanged"))
                {
                    try
                    {
                        // Override attribute names with property names
                        foreach (var propertyName in propertyNames)
                        {
                            if (propertyName.Equals(InteractionAttributeName.RemoteName, StringComparison.InvariantCultureIgnoreCase))
                                OnPropertyChanged("RemoteName");
                            else if (propertyName.Equals(InteractionAttributeName.State, StringComparison.InvariantCultureIgnoreCase))
                            {
                                OnPropertyChanged("State");
                                OnPropertyChanged("StateDescription");
                            }
                            else if (propertyName.Equals(InteractionAttributeName.StateDescription, StringComparison.InvariantCultureIgnoreCase))
                            {
                                OnPropertyChanged("StateDescription");
                                OnPropertyChanged("State");
                            }
                            else if (propertyName.Equals(InteractionAttributeName.UserQueueNames, StringComparison.InvariantCultureIgnoreCase))
                                OnPropertyChanged("UserName");
                            else if (propertyName.Equals(InteractionAttributeName.WorkgroupQueueName, StringComparison.InvariantCultureIgnoreCase))
                                OnPropertyChanged("WorkgroupQueueName");
                            else if (propertyName.Equals(VideoIntegrationAttributeNames.VideoConversationId, StringComparison.InvariantCultureIgnoreCase))
                                OnPropertyChanged("VidyoConversationId");
                            else if (propertyName.Equals(VideoIntegrationAttributeNames.VideoRoomUrl, StringComparison.InvariantCultureIgnoreCase))
                                OnPropertyChanged("VidyoRoomUrl");
                            else
                                OnPropertyChanged(propertyName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.Main.exception(ex);
                    }
                }
            }, null);
        }

        public void InvokeTransfer()
        {
            using (Trace.Main.scope())
            {
                try
                {
                    if (TransferTarget == null) return;

                    // Determine transfer target
                    QueueId queue;
                    switch (TransferTarget.Entry.LookupEntryType)
                    {
                        case LookupEntryType.User:
                        {
                            queue = new QueueId(QueueType.User, TransferTarget.Entry.EntryId);
                            break;
                        }
                        case LookupEntryType.Workgroup:
                        {
                            queue = new QueueId(QueueType.Workgroup, TransferTarget.Entry.EntryId);
                            break;
                        }
                        default:
                        {
                            throw new Exception("Invalid transfer target type: " + TransferTarget.Entry.LookupEntryType);
                        }
                    }

                    // Transfer
                    Trace.Main.note("Blind transfer to " + queue.ScopedName);
                    _interaction.BlindTransfer(queue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void MuteAudio(Participant participant, bool doMute)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    (new Thread(DoMuteAudio)).Start(new Tuple<Participant, bool>(participant, doMute));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void MuteVideo(Participant participant, bool doMute)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    (new Thread(DoMuteVideo)).Start(new Tuple<Participant, bool>(participant, doMute));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public void KickParticipant(Participant participant)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    (new Thread(DoKickParticipant)).Start(participant);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Trace.Main.exception(ex, ex.Message);
                }
            }
        }

        public override void Dispose()
        {
            try
            {
                _participantCheckerTimer.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex, ex.Message);
            }

            base.Dispose();
        }

        #endregion

    }
}
