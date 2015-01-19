using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using VidyoIntegration.TraceLib;
using Nancy.Helpers;
using VidyoIntegration.CicManagerLib;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.CicTypes.RequestClasses;
using VidyoIntegration.CommonLib.CicTypes.TransportClasses;
using VidyoIntegration.CommonLib.ConversationTypes;
using VidyoIntegration.CommonLib.Exceptions;
using VidyoIntegration.CommonLib.VidyoTypes;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using VidyoIntegration.ConversationManagerLib;
using Trace = VidyoIntegration.CommonLib.Trace;

namespace VidyoIntegration.CoreServiceLib
{
    public class CoreService
    {
        #region Private Fields

        private static CoreService _instance;
        private CicManager _cic = new CicManager();

        #endregion



        #region Public Properties

        public static CoreService Instance { get { return _instance ?? (_instance = new CoreService()); } }
        public bool IsConnected { get { return _cic.IsConnected; } }
        public string CicServer { get { return _cic.CicServer; } }
        public string CicUser { get { return _cic.CicUser; } }
        public string SessionManager { get { return _cic.SessionManager; } }
        public string ConnectionMessage { get { return _cic.ConnectionMessage; } }

        #endregion



        private CoreService()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Register for events
                    _cic.InteractionAssigned += CicOnInteractionAssigned;
                    _cic.InteractionDisconnected += CicOnInteractionDisconnected;
                    _cic.InteractionChanged += CicOnInteractionChanged;
                    _cic.InteractionQueueChanged += CicOnInteractionQueueChanged;
                    _cic.SessionConnectionLost += CicOnSessionConnectionLost;
                    _cic.VidyoNewConversationRequested += CicOnVidyoNewConversationRequested;

                    // Connect to CIC
                    _cic.Connect();

                    // Register for connection up
                    _cic.SessionConnectionUp += CicOnSessionConnectionUp;

                    // Reconstitute conversations
                    ReconstituteConversations();
                }
                catch (Exception ex)
                {
                    Trace.WriteRegisteredMessage(EventId.ApplicationInitializationCriticalFailure, ex.Message);
                    throw;
                }
            }
        }



        #region Private Methods

        private void ReconstituteConversations()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Load previous data
                    ConversationManager.LoadConversations();

                    // Check each conversation
                    var conversationsToDelete = new List<VidyoConversation>();
                    foreach (var conversation in ConversationManager.ConversationList)
                    {
                        using (Trace.Core.scope("Reconstitute " + conversation.ConversationId))
                        {
                            try
                            {
                                /* Store off the old interaction ID. This is necessary because of a race condition. When the old 
                                 * interaction is disconnected, but not deallocated, the disconnected event will be raised when 
                                 * the interaction reference is recreated and watches started. The disconnected event causes the 
                                 * conversation object to be cleaned up, which deletes the Vidyo room. That's bad. 
                                 * 
                                 * This workaround clears the old conversation ID so that the conversation object won't be found 
                                 * as a match when the disconnected event handler looks for a conversation by interaction ID. If
                                 * things are found to be in order, the interaction ID will be set back to the conversation.
                                 */
                                var oldInteractionId = conversation.InteractionId;
                                conversation.InteractionId = 0;
                                conversation.Save();


                                var needsInteraction = false;

                                // Does the interaction exist?
                                if (!_cic.InteractionExists(oldInteractionId))
                                {
                                    // No, but was the conversation waiting to be assigned?
                                    if (string.IsNullOrEmpty(conversation.UserOwner))
                                    {
                                        needsInteraction = true;
                                    }
                                    else
                                    {
                                        // It was assigned to a user, but are there people still in the room? (if yes, probably GI after switchover)
                                        if (VidyoServiceClient.GetParticipantCount(conversation.Room.RoomId).Count == 0)
                                            throw new ConversationReconstitutionException(
                                                "Interaction did not exist and nobody was in the Vidyo room.");

                                        // People are in the room!
                                        Trace.Core.status(
                                            "Interaction {} did not exist, but participants exist in room {}. Creating new interaction.",
                                            oldInteractionId, conversation.Room.RoomId);

                                        // Need to make an interaction
                                        needsInteraction = true;
                                    }
                                }

                                // Interaction exists, validate it
                                if (_cic.InteractionIsDisconnected(oldInteractionId))
                                {
                                    // Was it waiting in queue and unassigned?
                                    if (string.IsNullOrEmpty(_cic.GetUserQueueName(oldInteractionId)))
                                    {
                                        // Yes. Need to make an interaction
                                        needsInteraction = true;
                                    }
                                    else
                                    {
                                        // Nope. Clean up (was assigned to user, but was disconnected while we weren't looking)
                                        throw new ConversationReconstitutionException("Interaction " +
                                                                                     oldInteractionId +
                                                                                      " is disconnected.");
                                    }
                                }

                                // Need to make a new interaction for the conversation?
                                if (needsInteraction)
                                {
                                    // Make a new interaction
                                    //TODO: How to handle chat after switchover? Does the interaction ID change?
                                    var interactionId = _cic.MakeInteraction(conversation.InitializationParameters);
                                    if (interactionId == 0)
                                        throw new Exception("Failed to create new interaction!");

                                    // Update conversation
                                    conversation.InteractionId = interactionId;
                                    conversation.UpdateAttributes(_cic.GetAttributes(interactionId,
                                        conversation.AttributeDictionary.Select(kvp => kvp.Key).ToArray()));
                                    conversation.Save();

                                    // Done. Move on to next
                                    Trace.Core.status("Created new interaction {} for conversation", interactionId);
                                    continue;
                                }

                                // If we got here, everything is still in place
                                conversation.InteractionId = oldInteractionId;
                                conversation.Save();
                                Trace.Core.status("Conversation is still active");
                            }
                            catch (ConversationReconstitutionException ex)
                            {
                                Trace.Core.status("Marking conversation {} for cleanup. Reason: {}",
                                    conversation.ConversationId, ex.Message);
                                conversationsToDelete.Add(conversation);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteEventError(ex,
                                    "Failed to reconstitute conversation " + conversation.ConversationId + ". Message: " +
                                    ex.Message, EventId.GenericError);
                                conversationsToDelete.Add(conversation);
                            }
                        }
                    }

                    // Remove inactive conversations
                    foreach (var conversation in conversationsToDelete)
                    {
                        CleanupConversation(conversation);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in ReconstituteConversations: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private bool CleanupConversation(VidyoConversation conversation)
        {
            using (Trace.Cic.scope())
            {
                // Delete room
                if (!VidyoServiceClient.DeleteRoom(conversation.Room.RoomId))
                    Console.WriteLine("Error deleting room; deletion failed.");

                // Cleanup conversation
                ConversationManager.RemoveConversation(conversation);

                return true;
            }
        }

        private void CicOnInteractionDisconnected(long interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    Console.WriteLine("CicOnInteractionDisconnected -- {0}", interactionId);

                    // Get conference object
                    var conversation = ConversationManager.GetConversation(interactionId);
                    if (conversation == null)
                        throw new ConversationNotFoundException(interactionId);

                    CleanupConversation(conversation);
                }
                catch (ConversationNotFoundException ex)
                {
                    Trace.Cic.warning(ex.Message);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnInteractionDisconnected: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void CicOnInteractionAssigned(long interactionId, string agentName)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Validate data
                    if (string.IsNullOrEmpty(agentName))
                        agentName = "Vidyo Agent";

                    Console.WriteLine("CicOnInteractionAssigned -- {0}", interactionId);

                    // Get conference object
                    var conversation = ConversationManager.GetConversation(interactionId);
                    if (conversation == null)
                        throw new ConversationNotFoundException(interactionId);

                    // Done
                    Trace.Cic.verbose("Assignment process completed.");
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnInteractionAssigned: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void CicOnInteractionQueueChanged(long interactionId, string scopedQueueName, string userOwner)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Get conversation
                    var conversation = ConversationManager.GetConversation(interactionId);
                    if (conversation == null)
                        throw new ConversationNotFoundException(interactionId);

                    // Update queue
                    conversation.ScopedQueueName = scopedQueueName;
                    conversation.UserOwner = userOwner;
                    conversation.Save();
                }
                catch (ConversationNotFoundException ex)
                {
                    Trace.Cic.warning(ex.Message);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnInteractionQueueChanged: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void CicOnInteractionChanged(long interactionId, IDictionary<string, string> attributes)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Get conversation
                    var conversation = ConversationManager.GetConversation(interactionId);
                    if (conversation == null)
                        throw new ConversationNotFoundException(interactionId);

                    // Update attributes
                    conversation.UpdateAttributes(attributes);

                    // Action on hold
                    if (conversation.IsConversationMuted && !_cic.InteractionIsHeld(interactionId))
                    {
                        // Unmute
                        conversation.IsConversationMuted = false;
                        foreach (var participant in VidyoServiceClient.GetParticipants(conversation.Room.RoomId))
                        {
                            VidyoServiceClient.PerformAction(conversation.Room.RoomId, participant, RoomAction.MuteBoth,
                                false.ToString());
                        }
                    }
                    else if (!conversation.IsConversationMuted && _cic.InteractionIsHeld(interactionId))
                    {
                        // Mute
                        conversation.IsConversationMuted = true;
                        foreach (var participant in VidyoServiceClient.GetParticipants(conversation.Room.RoomId))
                        {
                            VidyoServiceClient.PerformAction(conversation.Room.RoomId, participant, RoomAction.MuteBoth,
                                true.ToString());
                        }
                    }
                }
                catch (ConversationNotFoundException ex)
                {
                    Trace.Cic.warning(ex.Message);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnInteractionChanged: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void CicOnSessionConnectionLost()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    Console.WriteLine("CicOnSessionConnectionLost - DO SOMETHING");
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnSessionConnectionLost: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void CicOnSessionConnectionUp()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Execute reload logic
                    Thread.Sleep(5000);
                    ReconstituteConversations();
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnSessionConnectionUp: " + ex.Message, EventId.GenericError);
                }
            }
        }

        private void CicOnVidyoNewConversationRequested(string username, string interactionId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (string.IsNullOrEmpty(interactionId))
                    {
                        CreateConversation(new CreateConversationRequest
                        {
                            QueueName = username,
                            QueueType = CicQueueType.User,
                            MediaTypeParameters = new GenericInteractionMediaTypeParameters
                            {
                                InitialState = GenericInteractionInitialState.Offering,
                                AdditionalAttributes = new List<KeyValuePair<string, string>>
                                {
                                    new KeyValuePair<string, string>(
                                        VideoIntegrationAttributeNames.VideoAutoAnswerOnReconstitution, "true"), // Causes client addin to call Interaction.Pickup() so the user doesn't have to answer it manually
                                    new KeyValuePair<string, string>("Eic_RemoteName",
                                        "Video chat created by user " + username)
                                }
                            }
                        });
                    }
                    else
                    {
                        long id;
                        long.TryParse(interactionId, out id);
                        AttachConversation(new AttachConversationRequest
                        {
                            InteractionId = id,
                            GuestName = "Video Chat Guest"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CicOnVidyoNewConversationRequested: " + ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion



        #region Public Methods

        public VidyoConversation CreateConversation(CreateConversationRequest request)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Ensure we're connected
                    _cic.Connect();

                    // Create parameters
                    VideoConversationInitializationParameters videoParameters;
                    if (request.MediaTypeParameters is CallbackInteractionMediaTypeParameters)
                    {
                        var requestParameters = request.MediaTypeParameters as CallbackInteractionMediaTypeParameters;
                        videoParameters = new CallbackVideoConversationInitializationParameters
                        {
                            CallbackMessage = requestParameters.CallbackMessage,
                            CallbackPhoneNumber = requestParameters.CallbackPhoneNumber,
                            ScopedQueueName = request.GetScopedQueueName()
                        };
                    }
                    else if (request.MediaTypeParameters is GenericInteractionMediaTypeParameters)
                    {
                        var requestParameters = request.MediaTypeParameters as GenericInteractionMediaTypeParameters;
                        videoParameters = new GenericInteractionVideoConversationInitializationParameters
                        {
                            InitialState = requestParameters.InitialState,
                            ScopedQueueName = request.GetScopedQueueName()
                        };
                    }
                    else
                    {
                        throw new Exception("Unsupported media type for new conversation: " +
                                            request.MediaTypeParameters.GetType());
                    }

                    // Create dictionary for interaction attributes
                    if (request.MediaTypeParameters.AdditionalAttributes == null)
                        request.MediaTypeParameters.AdditionalAttributes = new List<KeyValuePair<string, string>>();
                    videoParameters.AdditionalAttributes =
                        request.MediaTypeParameters.AdditionalAttributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    // Create conversation
                    var conversation = ConversationManager.CreateVideoConversation(videoParameters);

                    // Update additional attributes with conversation parameters
                    videoParameters.AdditionalAttributes[VideoIntegrationAttributeNames.VideoConversationId] =
                        conversation.ConversationId.ToString();
                    videoParameters.AdditionalAttributes[VideoIntegrationAttributeNames.VideoRoomId] =
                        conversation.Room.RoomId.ToString();
                    videoParameters.AdditionalAttributes[VideoIntegrationAttributeNames.VideoRoomUrl] =
                        conversation.RoomUrl;

                    // Create interaction
                    conversation.InteractionId = _cic.MakeInteraction(videoParameters);

                    // Sync attributes to the conversation
                    conversation.UpdateAttributes(_cic.GetAttributes(conversation.InteractionId,
                        videoParameters.AdditionalAttributes.Select(kvp => kvp.Key).ToArray()));

                    // Set the additional attributes on the interaction
                    _cic.SetAttributes(conversation.InteractionId, videoParameters.AdditionalAttributes);

                    // Return transport type
                    return conversation;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in CreateConversation: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public VidyoConversation AttachConversation(AttachConversationRequest request)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Ensure we're connected
                    _cic.Connect();

                    // Make sure it's valid as an attachment target
                    if (!_cic.InteractionExists(request.InteractionId))
                        throw new Exception("Unable to attach to interaction " + request.InteractionId +
                                            " because the interaction was not found.");
                    if (_cic.InteractionIsDisconnected(request.InteractionId))
                        throw new Exception("Unable to attach to interaction " + request.InteractionId +
                                            " because the interaction is disconnected.");
                    var interactionType = _cic.GetInteractionType(request.InteractionId);
                    if (interactionType != VideoConversationMediaType.GenericInteraction &&
                        interactionType != VideoConversationMediaType.Chat &&
                        interactionType != VideoConversationMediaType.Callback)
                        throw new Exception("Unable to attach to interaction " + request.InteractionId +
                                            " because the interaction type is invalid for attachment: " +
                                            interactionType);

                    // Create parameters
                    if (request.AdditionalAttributes == null)
                        request.AdditionalAttributes = new List<KeyValuePair<string, string>>();
                    var parameters = _cic.GetParametersFromInteraction(request.InteractionId);
                    parameters.AdditionalAttributes = request.AdditionalAttributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    // Create conversation
                    var conversation = ConversationManager.CreateVideoConversation(parameters);
                    conversation.InteractionId = request.InteractionId;

                    // Update additional attributes with conversation parameters
                    parameters.AdditionalAttributes[VideoIntegrationAttributeNames.VideoConversationId] =
                        conversation.ConversationId.ToString();
                    parameters.AdditionalAttributes[VideoIntegrationAttributeNames.VideoRoomId] =
                        conversation.Room.RoomId.ToString();
                    parameters.AdditionalAttributes[VideoIntegrationAttributeNames.VideoRoomUrl] =
                        conversation.RoomUrl;

                    // Sync attributes to the conversation
                    conversation.UpdateAttributes(parameters.AdditionalAttributes);

                    // Set the additional attributes on the interaction
                    _cic.SetAttributes(request.InteractionId, parameters.AdditionalAttributes);

                    // Send chat messages
                    if (interactionType == VideoConversationMediaType.Chat)
                    {
                        // Send text
                        _cic.SendChatText(request.InteractionId, "Video chat has been added to the chat");

                        // Send guest link
                        if (!string.IsNullOrEmpty(request.GuestName))
                            _cic.SendChatUrl(request.InteractionId,
                                new Uri(conversation.RoomUrl + "&guestName=" + HttpUtility.UrlEncode(request.GuestName)));
                    }

                    // Return transport type
                    return conversation;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in AttachConversation: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public bool CleanupConversation(Guid conversationId)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    // Find conversation
                    var conversation = ConversationManager.GetConversation(conversationId);
                    return conversation != null && CleanupConversation(conversation);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in CleanupConversation: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public List<QueueInfo> GetQueueInfo(GetQueueInfoRequest request)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    return request.Queues.Select(queue => _cic.GetQueueInfo(queue, request.WaitForData)).Where(info => info != null).ToList();
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetQueueInfo: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        #endregion
    }
}
