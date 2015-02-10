using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using VidyoIntegration.TraceLib;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Routing;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.CicTypes.RequestClasses;
using VidyoIntegration.CommonLib.CicTypes.TransportClasses;
using VidyoIntegration.CommonLib.CommonTypes;
using VidyoIntegration.CommonLib.VidyoTypes;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;
using VidyoIntegration.ConversationManagerLib;
using Timer = System.Timers.Timer;
using Trace = VidyoIntegration.CommonLib.Trace;

namespace VidyoIntegration.CoreServiceLib
{
    public class CoreRequestRouter : NancyModule
    {
        private const string UriPrefix = "/ininvid/v1";
        private static DateTime _initializedDateTime = DateTime.Now;
        private static Dictionary<string, int> _requestCounter = new Dictionary<string, int>();

        public CoreRequestRouter(IRouteCacheProvider routeCacheProvider)
        {
            // Section: /conversations
            #region POST /conversations
            Post[UriPrefix + "/conversations"] = _p =>
            {
                using (Trace.Cic.scope("POST /conversations"))
                {
                    try
                    {
                        UpdateCount("post /conversations");

                        var request = this.Bind<CreateConversationRequest>();

                        #region Validation

                        // Check Queue
                        if (string.IsNullOrEmpty(request.QueueName))
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: queueName"
                            };
                        if (request.QueueType == CicQueueType.None)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: queueType"
                            };

                        // Check media type parameters
                        if (request.MediaTypeParameters == null)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: mediaTypeParameters"
                            };

                        // Check type to block requests for new chat (unable to support at this time due to icelib constraints)
                        if (request.MediaTypeParameters is ChatInteractionMediaTypeParameters)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "MediaType of Chat is not supported for new conversation requests"
                            };

                        // Prevent null collections
                        if (request.MediaTypeParameters.AdditionalAttributes == null)
                            request.MediaTypeParameters.AdditionalAttributes = new List<KeyValuePair<string, string>>();

                        #endregion

                        // Create conversation
                        var conversation = CoreService.Instance.CreateConversation(request);
                        return conversation ?? (dynamic)new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = "Failed to create conversation"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Exception in POST /conversations: " + ex.Message, EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region POST /conversations/attach
            Post[UriPrefix + "/conversations/attach"] = _p =>
            {
                using (Trace.Cic.scope("POST /conversations/attach"))
                {
                    try
                    {
                        UpdateCount("post /conversations/attach");

                        var request = this.Bind<AttachConversationRequest>();

                        #region Validation

                        // Check interaction ID
                        if (request.InteractionId <= 0)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: interactionId"
                            };

                        // Prevent null collections
                        if (request.AdditionalAttributes == null)
                            request.AdditionalAttributes = new List<KeyValuePair<string, string>>();

                        #endregion

                        // Create conversation
                        var conversation = CoreService.Instance.AttachConversation(request);
                        return conversation ?? (dynamic)new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = "Failed to create conversation"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Exception in POST /conversations: " + ex.Message, EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region GET /conversations
            Get[UriPrefix + "/conversations"] = _p =>
            {
                using (Trace.Cic.scope("GET /conversations"))
                {
                    try
                    {
                        UpdateCount("get /conversations");

                        return ConversationManager.ConversationList;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Exception in GET /conversations: " + ex.Message, EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            #region GET /conversations/{conversationId}
            Get[UriPrefix + "/conversations/{conversationId}"] = _p =>
            {
                using (Trace.Cic.scope("GET /conversations/{conversationId}"))
                {
                    try
                    {
                        UpdateCount("get /conversations/{conversationId}");

                        // Validate input
                        if (((Guid)_p.conversationId) == Guid.Empty)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: conversationId"
                            };

                        // Return room
                        var conversation = ConversationManager.GetConversation((Guid)_p.conversationId);
                        return conversation ??
                               (dynamic)new Response
                               {
                                   StatusCode = HttpStatusCode.Gone,
                                   ReasonPhrase = "Conversation not found"
                               };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /conversations/{conversationId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Exception in GET /conversations/{conversationId}: " + ex.Message, EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region DELETE /conversations/{conversationId}
            Delete[UriPrefix + "/conversations/{conversationId}"] = _p =>
            {
                using (Trace.Cic.scope("DELETE /conversations/{conversationId}"))
                {
                    try
                    {
                        UpdateCount("delete /conversations/{conversationId}");

                        // Validate input
                        if (((Guid)_p.conversationId) == Guid.Empty)
                            return new Response
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                ReasonPhrase = "Value cannot be empty: conversationId"
                            };

                        // Cleanup
                        return CoreService.Instance.CleanupConversation((Guid) _p.conversationId)
                            ? new Response
                            {
                                StatusCode = HttpStatusCode.NoContent,
                                ReasonPhrase = "Conversation deleted"
                            }
                            : new Response
                            {
                                StatusCode = HttpStatusCode.Gone,
                                ReasonPhrase = "Unable to delete conversation"
                            };
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteEventError(ex, "Error in DELETE /conversations/{conversationId}: " + ex.Message,
                               EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            ReasonPhrase = "Invalid data format"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Exception in DELETE /conversations/{conversationId}: " + ex.Message, EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion

            // Section: /coreservice
            #region POST /coreservice/initialize
            Post[UriPrefix + "/coreservice/initialize"] = _p =>
            {
                using (Trace.Cic.scope("POST /coreservice/initialize"))
                {
                    try
                    {
                        UpdateCount("post /coreservice/initialize");

                        var x = CoreService.Instance;

                        return new Response
                        {
                            StatusCode = HttpStatusCode.NoContent,
                            ReasonPhrase = "Initialized"
                        };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Exception in POST /coreservice/initialize: " + ex.Message, EventId.GenericError);
                        return new Response
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            ReasonPhrase = ex.Message
                        };
                    }
                }
            };
            #endregion
            #region GET /coreservice/info
            Get[UriPrefix + "/coreservice/info"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /coreservice/info"))
                {
                    try
                    {
                        UpdateCount("get /coreservice/info");

                        var info = new CicInfo
                        {
                            ConversationCount = ConversationManager.ConversationList.Count,
                            IsConnectedToCic = CoreService.Instance.IsConnected,
                            CicServer = CoreService.Instance.CicServer,
                            CicUser = CoreService.Instance.CicUser,
                            Uptime = DateTime.Now.Subtract(_initializedDateTime),
                            SessionManager = CoreService.Instance.SessionManager,
                            ConnectionMessage = CoreService.Instance.ConnectionMessage,
                            RequestCounts = _requestCounter
                        };

                        return info;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /coreservice/info: " + ex.Message,
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
            #region GET /coreservice/info/routes
            Get[UriPrefix + "/coreservice/info/routes"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /coreservice/info/routes"))
                {
                    try
                    {
                        UpdateCount("get /coreservice/info/routes");

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
                        Trace.WriteEventError(ex, "Error in GET /coreservice/info/routes: " + ex.Message,
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

            // Section: /queues
            #region GET /queues/stats?queues[]={queue}
            Get[UriPrefix + "/queues/stats"] = _p =>
            {
                using (Trace.Vidyo.scope("GET /queues/stats?queues[]={queue}"))
                {
                    try
                    {
                        UpdateCount("get /queues/stats?queues[]={queue}");

                        var request = this.Bind<GetQueueInfoRequest>();

                        var result = CoreService.Instance.GetQueueInfo(request);
                        return result ??
                               (dynamic) new Response
                               {
                                   StatusCode = HttpStatusCode.InternalServerError,
                                   ReasonPhrase = "Failed to get queue info"
                               };
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteEventError(ex, "Error in GET /queues/stats?queues[]={queue}: " + ex.Message,
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
