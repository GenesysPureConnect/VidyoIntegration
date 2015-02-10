using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using VidyoIntegration.TraceLib;
using RestSharp;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;

namespace VidyoIntegration.CommonLib.VidyoTypes
{
    public static class VidyoServiceClient
    {

        #region Private Fields

        private static RestClient Client;
        private static string _baseUrl;

        #endregion



        #region Public Properties

        public static string BaseUrl
        {
            get { return _baseUrl; }
            set
            {
                if (string.IsNullOrEmpty(value)) return;

                // Store value
                _baseUrl = value;
                Console.WriteLine("VidyoServiceClient.BaseUrl = " + value);
                Trace.Common.status("VidyoServiceClient.BaseUrl = " + value);

                // Build URL for client
                var url = value.Trim(new[] { '/' });
                url += "/ininvid/v1";

                // Create new client from url
                Client = new RestClient(url);
            }
        }

        #endregion



        static VidyoServiceClient()
        {
            BaseUrl = ConfigurationProperties.VidyoServiceEndpointUri;
            if (string.IsNullOrEmpty(BaseUrl))
            {
                Console.WriteLine("VidyoServiceEndpointUri config file parameter was not set! VidyoServiceClient.BaseUrl must be set prior to making any service calls.");
                Trace.Common.warning("VidyoServiceEndpointUri config file parameter was not set! VidyoServiceClient.BaseUrl must be set prior to making any service calls.");
            }
        }



        #region Private Methods


        private static RestResponse<T> ExecuteRequest<T>(RestRequest request)
        {
            // Find the generic Execute<T> method
            var executeMethod =
                typeof(RestClient).GetMethods()
                    .FirstOrDefault(method => method.Name == "Execute" && method.IsGenericMethod);

            // Execute request
            var sw = new Stopwatch();
            sw.Start();
            var response =
                (RestResponse<T>)executeMethod.MakeGenericMethod(typeof(T)).Invoke(Client, new object[] { request });
            sw.Stop();
            Trace.Common.note("Executed request to {} in {}ms with result {} - {}", request.Resource,
                sw.ElapsedMilliseconds, response.StatusCode, response.StatusDescription);

            // Return
            return response;
        }

        private static IRestResponse ExecuteRequest(RestRequest request)
        {
            // Execute request
            var sw = new Stopwatch();
            sw.Start();
            var response = Client.Execute(request);
            sw.Stop();
            Trace.Common.note("Executed request to {} in {}ms with result {} - {}", request.Resource,
                sw.ElapsedMilliseconds, response.StatusCode, response.StatusDescription);

            // Return
            return response;
        }

        private static bool ValidateResponse(IRestResponse response)
        {
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
            {
                // Response was not OK
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                    Trace.Common.error("Error after request: {}", response.ErrorException);
                if (response.ErrorException != null) throw response.ErrorException;
                return false;
            }

            return true;
        }

        private static bool ValidateResponse(IRestResponse response, object data)
        {
            return ValidateResponse(response) && data != null;
        }

        //private static bool ValidateResponse<T>(IRestResponse response)
        //{
        //    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
        //    {
        //        // Response was not OK
        //        if (!string.IsNullOrEmpty(response.ErrorMessage))
        //            Trace.Common.error("Error after request: {}", response.ErrorException);
        //        if (response.ErrorException != null) throw response.ErrorException;
        //        return false;
        //    }

        //    return true;
        //}

        //private static bool ValidateResponse<T>(IRestResponse response, object data)
        //{
        //    return ValidateResponse(response) && data != null;
        //}

        #endregion



        #region Public Methods

        public static Room CreateRoom()
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms", Method.POST) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");

                    // Call service
                    var response = ExecuteRequest<Room>(request);

                    // Check response
                    if (!ValidateResponse(response, response.Data))
                        throw new Exception("Response data was not valid! Aborting!");

                    // Handle response
                    return response.Data;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in CreateRoom: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public static Room GetRoom(int roomId)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms/{roomId}", Method.GET) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");
                    request.AddUrlSegment("roomId", roomId.ToString());

                    // Call service
                    var response = ExecuteRequest<Room>(request);

                    // Check response
                    if (!ValidateResponse(response, response.Data))
                        throw new Exception("Response data was not valid! Aborting!");

                    // Handle response
                    return response.Data;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetRoom: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public static bool DeleteRoom(int roomId)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms/{roomId}", Method.DELETE) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");
                    request.AddUrlSegment("roomId", roomId.ToString());

                    // Call service
                    var response = ExecuteRequest(request);

                    // Check response
                    if (!ValidateResponse(response))
                        throw new Exception("Response data was not valid! Aborting!");

                    // Handle response
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetRoom: " + ex.Message, EventId.GenericError);
                    return false;
                }
            }
        }

        public static ParticipantCount GetParticipantCount(int roomId)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms/{roomId}/participantCount", Method.GET) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");
                    request.AddUrlSegment("roomId", roomId.ToString());

                    // Call service
                    var response = ExecuteRequest<ParticipantCount>(request);

                    // Check response
                    if (!ValidateResponse(response, response.Data))
                        throw new Exception("Response data was not valid! Aborting!");

                    // Handle response
                    return response.Data;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetParticipantCount: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public static ReadOnlyCollection<Participant> GetParticipants(int roomId)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms/{roomId}/participants", Method.GET) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");
                    request.AddUrlSegment("roomId", roomId.ToString());

                    // Call service
                    var response = ExecuteRequest<List<Participant>>(request);

                    // Check response
                    if (!ValidateResponse(response, response.Data))
                        throw new Exception("Response data was not valid! Aborting!");

                    // Handle response
                    return new ReadOnlyCollection<Participant>(response.Data);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in GetParticipants: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public static void MuteAudio(int roomId, Participant participant, bool muteOn)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    PerformAction(roomId, participant, RoomAction.MuteAudio, muteOn.ToString());
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in MuteAudio: " + ex.Message, EventId.GenericError);
                    //return null;
                }
            }
        }

        public static void MuteVideo(int roomId, Participant participant, bool muteOn)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    PerformAction(roomId, participant, RoomAction.MuteVideo, muteOn.ToString());
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in MuteAudio: " + ex.Message, EventId.GenericError);
                    //return null;
                }
            }
        }

        public static void PerformAction(int roomId, Participant participant, RoomAction action, string data)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms/{roomId}/actions/{participantId}", Method.PATCH) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");
                    request.AddUrlSegment("roomId", roomId.ToString());
                    request.AddUrlSegment("participantId", participant.ParticipantId.ToString());
                    request.AddBody(new RoomActionRequest
                    {
                        Action = action,
                        Data = data
                    });

                    // Call service
                    var response = ExecuteRequest<List<Participant>>(request);

                    // Check response
                    if (!ValidateResponse(response))
                        throw new Exception("Response data was not valid! Aborting!");
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in PerformAction: " + ex.Message, EventId.GenericError);
                    //return null;
                }
            }
        }

        public static void KickParticipant(int roomId, Participant participant)
        {
            using (Trace.Common.scope())
            {
                try
                {
                    // Build request
                    var request = new RestRequest("rooms/{roomId}/participants/{participantId}", Method.DELETE) { RequestFormat = DataFormat.Json };
                    request.AddHeader("Content-Type", "application/json");
                    request.AddUrlSegment("roomId", roomId.ToString());
                    request.AddUrlSegment("participantId", participant.ParticipantId.ToString());

                    // Call service
                    var response = ExecuteRequest<List<Participant>>(request);

                    // Check response
                    if (!ValidateResponse(response))
                        throw new Exception("Response data was not valid! Aborting!");

                    // Handle response
                    //return new ReadOnlyCollection<Participant>(response.Data);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Exception in MuteAudio: " + ex.Message, EventId.GenericError);
                    //return null;
                }
            }
        }

        #endregion
    }
}
