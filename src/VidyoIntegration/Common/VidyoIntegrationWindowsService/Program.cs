using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using Nancy.Hosting.Self;
using RestSharp;
using VidyoIntegration.CommonLib;
using VidyoIntegration.TraceLib;
using Trace = VidyoIntegration.CommonLib.Trace;

namespace VidyoIntegrationWindowsService
{
    public class Program : ServiceBase
    {
        private NancyHost _host;

        public Program()
        {
            ServiceName = "VidyoIntegration";
        }

        private static void Main()
        {
            Run(new Program());
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            try
            {
                Trace.Initialize(typeof (VidyoEventId), "VidyoIntegration");

                var uriList = new List<Uri>();

                // Add CIC url
                if (!string.IsNullOrEmpty(ConfigurationProperties.CicServiceEndpointUri))
                    uriList.Add(new Uri(ConfigurationProperties.CicServiceEndpointUri));

                // Add Vidyo URL
                if (!string.IsNullOrEmpty(ConfigurationProperties.VidyoServiceEndpointUri) &&
                    !ConfigurationProperties.VidyoServiceEndpointUri.Equals(
                        ConfigurationProperties.CicServiceEndpointUri))
                    uriList.Add(new Uri(ConfigurationProperties.VidyoServiceEndpointUri));

                // Make sure we got at least one URI
                if (uriList.Count == 0)
                    throw new Exception("At least one endpoint URI must be specified!");

                // Dynamically run services as the user that is executing this application
                var username = (string.IsNullOrEmpty(Environment.UserDomainName)
                    ? Environment.MachineName
                    : Environment.UserDomainName)
                               + "\\" + Environment.UserName;
                var hostConfig = new HostConfiguration
                {
                    UrlReservations = new UrlReservations
                    {
                        CreateAutomatically = true,
                        User = username
                    }
                };

                // Create the Nancy host
                _host = new NancyHost(hostConfig, uriList.ToArray());

                // Finds any public services in the module and starts them
                _host.Start();

                // Call the initialize method
                try
                {
                    var url = ConfigurationProperties.CicServiceEndpointUri;
                    url = url.Trim(new[] {'/'});
                    url += "/ininvid/v1";
                    var client = new RestClient(url);
                    var request = new RestRequest("coreservice/initialize", Method.POST);
                    var response = client.Execute(request);

                    if (response.StatusCode != HttpStatusCode.NoContent)
                    {
                        var msg = "Response from initialization was \"" + ((int) response.StatusCode) + " " +
                                  response.StatusDescription + "\"";
                        Trace.WriteEventMessage(msg, EventLogEntryType.Warning, EventId.GenericWarning);
                    }
                }
                catch (Exception ex)
                {
                    var msg = "Error initializing service! " + ex.Message;
                    Trace.WriteEventError(ex, msg, EventId.GenericError);
                }

                // Done
                Trace.WriteRegisteredMessage(EventId.ApplicationInitialized,
                    "Registered endpoints: " + Environment.NewLine +
                    uriList.Select(uri => uri.ToString()).Aggregate((a, b) => a + Environment.NewLine + b));
            }
            catch (Exception ex)
            {
                Trace.WriteEventError(ex, "Error initializing services: " + ex.Message,
                    EventId.ApplicationInitializationCriticalFailure);

                // Throw to cause the service to stop/fail to start
                throw;
            }
        }

        protected override void OnStop()
        {
            base.OnStop();

            try
            {
                _host.Stop();
                _host.Dispose();
                Trace.WriteRegisteredMessage(VidyoEventId.ApplicationShutdown, "Nancy host stopped, service will now shut down.");
            }
            catch (Exception ex)
            {
                Trace.WriteEventError(ex, "Error stopping services: " + ex.Message,
                    EventId.GenericError);
            }
        }
    }
}
