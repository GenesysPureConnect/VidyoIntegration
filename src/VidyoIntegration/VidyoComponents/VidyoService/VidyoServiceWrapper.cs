using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Web;
using ININ.PSO.PsoTrace;
using Newtonsoft.Json;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using VidyoIntegration.VidyoService.VidyoPortalAdminService;
using VidyoIntegration.VidyoService.VidyoPortalGuestService;
using VidyoIntegration.VidyoService.VidyoPortalUserService;
using DeleteRoomRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.DeleteRoomRequest;
using Filter = VidyoIntegration.VidyoService.VidyoPortalAdminService.Filter;
using GetParticipantsRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.GetParticipantsRequest;
using LeaveConferenceRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.LeaveConferenceRequest;
using MuteAudioRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.MuteAudioRequest;
using Room = VidyoIntegration.CommonLib.VidyoTypes.TransportClasses.Room;
using RoomMode = VidyoIntegration.VidyoService.VidyoPortalAdminService.RoomMode;
using StartVideoRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.StartVideoRequest;
using StopVideoRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.StopVideoRequest;
using Trace = VidyoIntegration.CommonLib.Trace;
using UnmuteAudioRequest = VidyoIntegration.VidyoService.VidyoPortalAdminService.UnmuteAudioRequest;

namespace VidyoIntegration.VidyoService
{
    internal class VidyoServiceWrapper
    {
        #region Private

        private VidyoPortalGuestServicePortTypeClient _vidyoGuestService;
        private VidyoPortalUserServicePortTypeClient _vidyoUserService;
        private VidyoPortalAdminServicePortTypeClient _vidyoAdminService;
        private readonly Random _randomNumberGenerator = new Random();
        private readonly Dictionary<int, Room> _rooms = new Dictionary<int, Room>(); 

        private object _getRoomLocker = new object();

        #endregion



        #region Public



        #endregion



        #region Constructor

        internal VidyoServiceWrapper()
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    // Admin service
                    _vidyoAdminService = new VidyoPortalAdminServicePortTypeClient(MakeBinding(),
                        new EndpointAddress(ConfigurationProperties.VidyoPortalAdminServicePort.Address));
                    _vidyoAdminService.ClientCredentials.UserName.UserName = ConfigurationProperties.VidyoAdminUsername;
                    _vidyoAdminService.ClientCredentials.UserName.Password = ConfigurationProperties.VidyoAdminPassword;
                    _vidyoAdminService.ClientCredentials.SupportInteractive = false;
                    _vidyoAdminService.ChannelFactory.CreateChannel();

                    // Guest service
                    _vidyoGuestService = new VidyoPortalGuestServicePortTypeClient(MakeBinding(),
                        new EndpointAddress(ConfigurationProperties.VidyoPortalGuestServicePort.Address));
                    _vidyoGuestService.ClientCredentials.UserName.UserName = ConfigurationProperties.VidyoAdminUsername;
                    _vidyoGuestService.ClientCredentials.UserName.Password = ConfigurationProperties.VidyoAdminPassword;
                    _vidyoGuestService.ClientCredentials.SupportInteractive = false;
                    _vidyoGuestService.ChannelFactory.CreateChannel();

                    // User service
                    _vidyoUserService = new VidyoPortalUserServicePortTypeClient(MakeBinding(),
                        new EndpointAddress(ConfigurationProperties.VidyoPortalUserServicePort.Address));
                    _vidyoUserService.ClientCredentials.UserName.UserName = ConfigurationProperties.VidyoAdminUsername;
                    _vidyoUserService.ClientCredentials.UserName.Password = ConfigurationProperties.VidyoAdminPassword;
                    _vidyoUserService.ClientCredentials.SupportInteractive = false;
                    _vidyoUserService.ChannelFactory.CreateChannel();

                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error constructing VidyoServiceWrapper: " + ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion



        #region Private Methods

        private CustomBinding MakeBinding()
        {
            var binding = new BasicHttpBinding
            {
                CloseTimeout = new TimeSpan(0, 1, 0),
                OpenTimeout = new TimeSpan(0, 1, 0),
                ReceiveTimeout = new TimeSpan(0, 10, 0),
                SendTimeout = new TimeSpan(0, 1, 0),
                HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
                MaxBufferSize = 65536,
                MaxBufferPoolSize = 524288,
                MaxReceivedMessageSize = 65536,
                MessageEncoding = WSMessageEncoding.Text,
                TextEncoding = Encoding.UTF8,
                TransferMode = TransferMode.Buffered
            };

            // Set security options
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            binding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;

            // Turn off keep alive
            var customBinding = new CustomBinding(binding);
            foreach (var bindingElement in customBinding.Elements.OfType<HttpTransportBindingElement>())
            {
                bindingElement.KeepAliveEnabled = false;
            }

            return customBinding;
        }

        private int MakeRandomRoomExtension()
        {
            return _randomNumberGenerator.Next(900000000, 999999999); 
        }

        private int MakeRandomRoomPin()
        {
            return _randomNumberGenerator.Next(100000, 999999); 
        }

        private string MakeGuestJoinUrl(string portalUri, string roomKey, string guestName, string pin, string roomPin)
        {
            return
                ConfigurationProperties.VidyoWebBaseUrl +
                "?portalUri=" + HttpUtility.UrlEncode(portalUri) +
                "&roomKey=" + HttpUtility.UrlEncode(roomKey) +
                "&guestName=" + HttpUtility.UrlEncode(guestName) +
                "&pin=" + HttpUtility.UrlEncode(pin) +
                "&roomPin=" + HttpUtility.UrlEncode(roomPin);
        }

        private Room MakeRoom(VidyoIntegration.VidyoService.VidyoPortalAdminService.Room room)
        {
            return new Room
            {
                RoomId = room.roomID,
                Name = room.name,
                Extension = room.extension,
                Pin = room.RoomMode.roomPIN,
                RoomUrl = room.RoomMode.roomURL
            };
        }

        private Participant GetParticipant(int roomId, int participantId)
        {
            // Get participants
            var participants = GetParticipants(roomId);

            // Find participant
            return participants.FirstOrDefault(p => p.ParticipantId == participantId);
        }

        private void MuteAudio(int roomId, Participant participant, bool doMute)
        {
            if (doMute)
            {
                Trace.Vidyo.note("Muting audio for {}", participant);
                _vidyoAdminService.muteAudio(new MuteAudioRequest
                {
                    conferenceID = roomId,
                    participantID = participant.ParticipantId
                });
            }
            else
            {
                Trace.Vidyo.note("Unmuting audio for {}", participant);
                _vidyoAdminService.unmuteAudio(new UnmuteAudioRequest
                {
                    conferenceID = roomId,
                    participantID = participant.ParticipantId
                });
            }
        }

        private void MuteVideo(int roomId, Participant participant, bool doMute)
        {
            if (doMute)
            {
                Trace.Vidyo.note("Muting video for {}", participant);
                _vidyoAdminService.stopVideo(new StopVideoRequest
                {
                    conferenceID = roomId,
                    participantID = participant.ParticipantId
                });
            }
            else
            {
                Trace.Vidyo.note("Unmuting video for {}", participant);
                _vidyoAdminService.startVideo(new StartVideoRequest
                {
                    conferenceID = roomId,
                    participantID = participant.ParticipantId
                });
            }
        }

        #endregion



        #region Internal Methods

        internal Room AddRoom()
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    /* 
                     ╔════════════════════════════════════════════════════════════════════════════════════╗
                     ║ TECH NOTE -- Add Room Process                                                      ║
                     ╟────────────────────────────────────────────────────────────────────────────────────╢
                     ║ It is necessary to query the Vidyo service to get the details of the new room      ║
                     ║ because the call to addRoom does not return the information about the room.        ║
                     ║ The roomID specified in the request is ignored by the Vidyo server and it          ║
                     ║ auto-generates a roomID. The roomID is required by other actions, so it must be    ║
                     ║ retrieved now to provide it to the requestor. Vidyo has confirmed that this is     ║
                     ║ the expected behavior and that this method is the correct usage. An enhancement    ║
                     ║ request has been logged, but it is not on their roadmap as of 7/16/2014.           ║
                     ╚════════════════════════════════════════════════════════════════════════════════════╝
                     */

                    // Get a random extension for the room and create the name
                    var ext = MakeRandomRoomExtension();
                    var roomName = "Vidyo Integration Room (" + ext + ")";

                    // Invoke the Vidyo service to create the room
                    Trace.Vidyo.note("Creating room \"{}\"...", roomName);
                    
                    var sw = new Stopwatch();
                    sw.Start();
                    var addRoomResponse = _vidyoAdminService.addRoom(new AddRoomRequest
                    {
                        room = new VidyoIntegration.VidyoService.VidyoPortalAdminService.Room
                        {
                            description = "Vidyo Integration Room",
                            extension = ext.ToString(),
                            groupName = ConfigurationProperties.VidyoRoomGroup,
                            name = roomName,
                            ownerName = ConfigurationProperties.VidyoRoomOwner,
                            roomIDSpecified = true,
                            //roomID = ext,
                            RoomMode = new RoomMode
                            {
                                hasModeratorPIN = false,
                                hasPIN = true,
                                isLocked = false,
                                roomPIN = MakeRandomRoomPin().ToString()
                            },
                            RoomType = RoomType.Public
                        }
                    });
                    sw.Stop();
                    Trace.Vidyo.note("Room added in {}ms", sw.ElapsedMilliseconds);

                    // Get the room details
                    sw.Restart();
                    var room = _vidyoAdminService.getRooms(new GetRoomsRequest
                    {
                        Filter = new Filter
                        {
                            query = ext.ToString()
                        }
                    });
                    sw.Stop();
                    Trace.Vidyo.note("Room retrieved in {}ms", sw.ElapsedMilliseconds);

                    // Verify room query result
                    if (room.total > 1)
                        Trace.Main.warning("More than one room returned from room query!");
                    if (room.total == 0)
                        throw new Exception("No rooms were returned from room query!");
                    var verifiedRoom = room.room.FirstOrDefault(r => r.extension == ext.ToString() && r.name == roomName);
                    if (verifiedRoom == null)
                        throw new Exception("Failed to validate room result!");

                    // Create response object
                    var transportRoom = MakeRoom(verifiedRoom);
                    lock (_getRoomLocker)
                    {
                        _rooms.Add(transportRoom.RoomId, transportRoom);
                    }

                    // Return the Room
                    return transportRoom;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in AddRoom: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        internal Room GetRoom(int roomId)
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    lock (_getRoomLocker)
                    {
                        // Return from cache if we have it
                        if (_rooms.ContainsKey(roomId)) return _rooms[roomId];

                        // Get room from API
                        Trace.Vidyo.note("Getting room from API. RoomId={}", roomId);

                        var sw = new Stopwatch();
                        sw.Start();
                        var room = _vidyoAdminService.getRoom(new GetRoomRequest
                        {
                            roomID = roomId,
                        });
                        sw.Stop();
                        Trace.Vidyo.note("Got room in {}ms", sw.ElapsedMilliseconds);

                        // Update cache
                        var cacheRoom = MakeRoom(room.room);
                        _rooms.Add(cacheRoom.RoomId, cacheRoom);

                        // Return room
                        return cacheRoom;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetGuest: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        internal Dictionary<int, Room> GetRooms()
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    lock (_getRoomLocker)
                    {
                        // Return from cache if we have it
                        return _rooms;

                        // Get room from API
                        //Trace.Vidyo.note("Getting room from API. RoomId={}", roomId);
                        //OpenAdminService();
                        //var sw = new Stopwatch();
                        //sw.Start();
                        //var room = _vidyoAdminService.getRoom(new GetRoomRequest
                        //{
                        //    roomID = roomId
                        //});
                        //sw.Stop();
                        //Trace.Vidyo.note("Got room in {}ms", sw.ElapsedMilliseconds);

                        //// Update cache
                        //var cacheRoom = MakeRoom(room.room);
                        //_rooms.Add(cacheRoom.RoomId, cacheRoom);

                        //// Return room
                        //return cacheRoom;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetGuest: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        /// <summary>
        /// Deletes the room and all participants
        /// </summary>
        /// <param name="roomId"></param>
        internal bool DeleteRoom(int roomId)
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    // See if the room is valid first
                    try
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();
                        var room = _vidyoAdminService.getRoom(new GetRoomRequest
                        {
                            roomID = roomId,
                        });
                        stopwatch.Stop();
                        Trace.Vidyo.note("Got room in {}ms", stopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        // If the room does not exist, 
                        Trace.Vidyo.warning("Room {} does not exist! Error: ", roomId, ex.Message);
                        return true;
                    }

                    // Kick all participants
                    var participants = GetParticipants(roomId);
                    if (participants != null)
                    {
                        foreach (var participant in participants)
                        {
                            KickParticipant(roomId, participant);
                        }
                    }
                    else
                    {
                        Trace.WriteEventMessage("Failed to get participant list!", EventLogEntryType.Warning,
                            EventId.GenericWarning);
                    }

                    // Delete the room
                    var sw = new Stopwatch();
                    sw.Start();
                    _vidyoAdminService.deleteRoom(new DeleteRoomRequest
                    {
                        roomID = roomId
                    });
                    sw.Stop();
                    Trace.Vidyo.note("Deleted room {} in {}ms", roomId, sw.ElapsedMilliseconds);

                    // Remove from cache list
                    lock (_getRoomLocker)
                    {
                        if (_rooms.ContainsKey(roomId))
                            _rooms.Remove(roomId);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in DeleteRoom: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }


        internal List<Participant> GetParticipants(int roomId)
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    // Get current participants
                    var sw = new Stopwatch();
                    sw.Start();
                    var participants = _vidyoAdminService.getParticipants(new GetParticipantsRequest
                    {
                        conferenceID = roomId
                    });
                    sw.Stop();

                    if (participants.Entity == null)
                        return new List<Participant>();

                    // Filter list to only real entities (not sure why bogus entries can be returned, but it's possible)
                    var confirmedParticipants =
                        participants.Entity.Where(participant => participant.participantID != null).ToList();
                    Trace.Vidyo.note("Found {} participants in {}ms", confirmedParticipants.Count,
                        sw.ElapsedMilliseconds);

                    // Manipulate into return type
                    var guests = new List<Participant>();
                    foreach (var entity in confirmedParticipants)
                    {
                        // Create object to return
                        guests.Add(new Participant
                        {
                            EntityId = entity.entityID,
                            ParticipantId = entity.participantID.HasValue ? (int) entity.participantID : -1,
                            EntityType = entity.EntityType.ToString(),
                            DisplayName = entity.displayName
                        });
                    }
                    Trace.Vidyo.verbose("Participants list: {}", JsonConvert.SerializeObject(guests));
                    return guests;
                }
                catch (CommunicationException ex)
                {
                    Console.WriteLine("Exception in GetParticipants: " + ex.Message);
                    Trace.WriteEventError(ex, "Exception in GetParticipants: " + ex.Message, EventId.GenericError);
                    return null;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetParticipants: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        internal bool KickParticipant(int roomId, int participantId)
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    // Find participant
                    var participant = GetParticipant(roomId, participantId);
                    if (participant == null)
                    {
                        Trace.Vidyo.note("Failed to find participant with participantId {} for room {}", participantId, roomId);
                        return false;
                    }

                    // Kick
                    return KickParticipant(roomId, participant);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in KickParticipant: " + ex.Message, EventId.GenericError);
                    return false;
                }

            }
        }

        internal bool KickParticipant(int roomId, Participant participant)
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    if (participant == null) return false;
                    if (roomId <= 0) return false;

                    // Kick
                    var sw = new Stopwatch();
                    sw.Start();
                    _vidyoAdminService.leaveConference(new LeaveConferenceRequest
                    {
                        conferenceID = roomId,
                        participantID = participant.ParticipantId
                    });
                    sw.Stop();
                    Trace.Vidyo.note("Evicted {} in {}ms", participant.DisplayName, sw.ElapsedMilliseconds);

                    // Success
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in KickParticipant: " + ex.Message, EventId.GenericError);
                    return false;
                }

            }
        }

        internal bool PerformAction(int roomId, int participantId, RoomAction action, string actionData)
        {
            using (Trace.Vidyo.scope())
            {
                try
                {
                    // Find participant
                    var participant = GetParticipant(roomId, participantId);
                    if (participant == null)
                    {
                        Trace.Vidyo.note("Failed to find participant with participantId {} for room {}", participantId, roomId);
                        return false;
                    }

                    // Take action
                    switch (action)
                    {
                        case RoomAction.MuteAudio:
                        {
                            bool doMute;
                            if (!bool.TryParse(actionData, out doMute))
                                throw new Exception("Failed to parse boolean data: " + actionData + " for action " +
                                                    action);

                            MuteAudio(roomId, participant, doMute);
                            return true;
                        }
                        case RoomAction.MuteVideo:
                        {
                            bool doMute;
                            if (!bool.TryParse(actionData, out doMute))
                                throw new Exception("Failed to parse boolean data: " + actionData + " for action " +
                                                    action);

                            MuteVideo(roomId, participant, doMute);
                            return true;
                        }
                        case RoomAction.MuteBoth:
                        {
                            bool doMute;
                            if (!bool.TryParse(actionData, out doMute))
                                throw new Exception("Failed to parse boolean data: " + actionData + " for action " +
                                                    action);

                            MuteVideo(roomId, participant, doMute);
                            MuteAudio(roomId, participant, doMute);
                            return true;
                        }
                        default:
                        {
                            Trace.Vidyo.warning("Unexpected action: " + action);
                            break;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in PerformAction: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        #endregion

    }
}
