using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using VidyoIntegration.TraceLib;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Routing;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.CommonTypes;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using Timer = System.Timers.Timer;

namespace VidyoIntegration.VidyoService
{
    public class VidyoRequestRouter : NancyModule
    {
        private const string UriPrefix = "/ininvid/v1";
        private static Dictionary<string, int> _requestCounter = new Dictionary<string, int>();

        private static readonly VidyoServiceWrapper Vidyo = new VidyoServiceWrapper();

        public VidyoRequestRouter(IRouteCacheProvider routeCacheProvider)
        {
            // Section: /rooms
            #region POST /rooms
            Post[UriPrefix + "/rooms"] = _p =>
            {
                using (Trace.Vidyo.scope("POST /rooms"))
                {
                    try
                    {
                        UpdateCount("post /rooms");

                        var room = Vidyo.AddRoom();
                        return room ?? (dynamic) new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = "Failed to create room"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in POST /rooms: " + ex.Message,
                                  EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region GET /rooms
            Get[UriPrefix + "/rooms"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /rooms"))
                {
                    try
                    {
                        UpdateCount("get /rooms");

                        var rooms = Vidyo.GetRooms();
                        return rooms ?? (dynamic)new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = "Failed to get rooms"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            #region GET /rooms/{roomId}
            Get[UriPrefix + "/rooms/{roomId}"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /rooms/{roomId}"))
                {
                    try
                    {
                        UpdateCount("get /rooms/{roomId}");

                        // Validate input
                        if (_p.roomId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: roomId"
                            };

                        var room = Vidyo.GetRoom(_p.roomId);
                        return room ?? new Response
                        {
                            StatusCode = HttpStatusCode.Gone,
                            ReasonPhrase = "Room not found"
                        };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms/{roomId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms/{roomId}: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region DELETE /rooms/{roomId}
            Delete[UriPrefix + "/rooms/{roomId}"] = _p =>
            {
                using (Trace.Vidyo.scope("DELETE /rooms/{roomId}"))
                {
                    try
                    {
                        UpdateCount("delete /rooms/{roomId}");

                        // Validate input
                        if (_p.roomId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: roomId"
                            };

                        return Vidyo.DeleteRoom(_p.roomId)
                            ? new Response
                            {
                                StatusCode = HttpStatusCode.NoContent,
                                ReasonPhrase = "Room deleted"
                            }
                            : new Response
                            {
                                StatusCode = HttpStatusCode.Gone,
                                ReasonPhrase = "Unable to delete room"
                            };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in DELETE /rooms/{roomId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in DELETE /rooms/{roomId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            #region GET /rooms/{roomId}/participants
            Get[UriPrefix + "/rooms/{roomId}/participants"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /rooms/{roomId}/participants"))
                {
                    try
                    {
                        UpdateCount("get /rooms/{roomId}/participants");

                        // Validate input
                        if (_p.roomId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: roomId"
                            };

                        var participants = Vidyo.GetParticipants(_p.roomId);
                        return participants ?? new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = "Unable to retrieve participants"
                        };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms/{roomId}/participants: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms/{roomId}/participants: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region DELETE /rooms/{roomId}/participants/{participantId}
            Delete[UriPrefix + "/rooms/{roomId}/participants/{participantId}"] = _p =>
            {
                using (Trace.Vidyo.scope("DELETE /rooms/{roomId}/participants/{participantId}"))
                {
                    try
                    {
                        UpdateCount("delete /rooms/{roomId}/participants/{participantId}");

                        // Validate input
                        if (_p.roomId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: roomId"
                            };
                        if (_p.participantId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: participantId"
                            };

                        return Vidyo.KickParticipant(_p.roomId, _p.participantId)
                            ? new Response
                            {
                                StatusCode = HttpStatusCode.NoContent,
                                ReasonPhrase = "Participant kicked"
                            }
                            : new Response
                            {
                                StatusCode = HttpStatusCode.Gone,
                                ReasonPhrase = "Unable to kick participant"
                            };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in DELETE /rooms/{roomId}/participants/{participantId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in DELETE /rooms/{roomId}/participants/{participantId}: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            #region GET /rooms/{roomId}/participantCount
            Get[UriPrefix + "/rooms/{roomId}/participantCount"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /rooms/{roomId}/participantCount"))
                {
                    try
                    {
                        UpdateCount("get /rooms/{roomId}/participantCount");

                        // Validate input
                        if (_p.roomId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: roomId"
                            };

                        var participants = Vidyo.GetParticipants(_p.roomId);
                        return participants == null
                            ? (dynamic) new Response
                            {
                                StatusCode = HttpStatusCode.InternalServerError,
                                ReasonPhrase = "Unable to retrieve participants"
                            }
                            : new ParticipantCount {Count = participants.Count};
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms/{roomId}/participantCount: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /rooms/{roomId}/participantCount: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            #region PATCH /rooms/{roomId}/actions/{participantId}
            Patch[UriPrefix + "/rooms/{roomId}/actions/{participantId}"] = _p =>
            {
                using (Trace.Vidyo.scope("PATCH /rooms/{roomId}/actions/{participantId}"))
                {
                    try
                    {
                        UpdateCount("patch /rooms/{roomId}/actions/{participantId}");

                        var request = this.Bind<RoomActionRequest>();

                        // Validate input
                        if (_p.roomId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: roomId"
                            };
                        if (_p.participantId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: participantId"
                            };
                        if (request.Action == RoomAction.None)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: action"
                            };
                        if (string.IsNullOrEmpty(request.Data))
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: data"
                            };

                        return Vidyo.PerformAction(_p.roomId, _p.participantId, request.Action, request.Data)
                            ? new Response
                            {
                                StatusCode = HttpStatusCode.NoContent,
                                ReasonPhrase = "Action performed"
                            }
                            : new Response
                            {
                                StatusCode = HttpStatusCode.InternalServerError,
                                ReasonPhrase = "Failed to perform action"
                            };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in PATCH /rooms/{roomId}/actions/{participantId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in PATCH /rooms/{roomId}/actions/{participantId}: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            // Section: /vidyoservice
            #region GET /vidyoservice/info
            Get[UriPrefix + "/vidyoservice/info"] = _p =>
            {
                try
                {
                    UpdateCount("get /vidyoservice/info");

                    var info = new VidyoInfo
                    {
                        RequestCounts = _requestCounter
                    };

                    return info;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in GET /vidyoservice/info: " + ex.Message,
                        EventId.GenericError);
                    return new Response
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        ReasonPhrase = ex.Message
                    };
                }
            };
            #endregion
            #region GET /vidyoservice/info/routes
            Get[UriPrefix + "/vidyoservice/info/routes"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /vidyoservice/info/routes"))
                {
                    try
                    {
                        UpdateCount("get /vidyoservice/info/routes");

                        // Get cache of defined routes
                        var routes = routeCacheProvider.GetCache();
                        var serviceInfoList = new List<ServiceInfo>();

                        // Process routes
                        foreach (var route in routes)
                        {
                            // We only want info for this service
                            if (!(route.Key == GetType())) continue;

                            // Create ServiceInfo for the service
                            var serviceInfo = new ServiceInfo
                            {
                                ServiceName = route.Key.FullName,
                                Routes = new List<RouteInfo>()
                            };

                            // Add the collection of routes
                            serviceInfo.Routes.AddRange(route.Value.Select(mapping => new RouteInfo
                            {
                                Method = mapping.Item2.Method,
                                Path = mapping.Item2.Path
                            }));

                            // Add the ServiceInfo item to the list
                            serviceInfoList.Add(serviceInfo);
                        }
                        return serviceInfoList;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /vidyoservice/info/routes: " + ex.Message,
                            EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

        }

        private static void UpdateCount(string key)
        {
            if (!_requestCounter.ContainsKey(key))
                _requestCounter[key] = 0;
            _requestCounter[key]++;
        }
    }
}
