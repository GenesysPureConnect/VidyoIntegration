using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using VidyoIntegration.TraceLib;
using RestSharp;
using VidyoIntegration.CommonLib;
using Trace = VidyoIntegration.CommonLib.Trace;

namespace VidyoIntegrationTestConsole
{
    using System;
    using Nancy.Hosting.Self;

    internal class Program
    {
        private static void Main(string[] args)
        {
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

                // Start the Nancy host
                using (var host = new NancyHost(hostConfig, uriList.ToArray()))
                {
                    // Finds any public services in the module and starts them
                    host.Start();
                    Console.WriteLine("Your application is running on: ");
                    foreach (var uri in uriList)
                    {
                        Console.WriteLine(uri);
                    }

                    // Call the initialize method
                    try
                    {
                        Console.WriteLine("Initializing...");
                        var url = ConfigurationProperties.CicServiceEndpointUri;
                        url = url.Trim(new[] { '/' });
                        url += "/ininvid/v1";
                        var client = new RestClient(url);
                        var request = new RestRequest("coreservice/initialize", Method.POST);
                        var response = client.Execute(request);

                        if (response.StatusCode != HttpStatusCode.NoContent)
                        {
                            var msg = "Response from initialization was \"" + ((int)response.StatusCode) + " " +
                                      response.StatusDescription + "\"";
                            Console.WriteLine(msg);
                            Trace.WriteEventMessage(msg, EventLogEntryType.Warning, EventId.GenericWarning);
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = "Error initializing service! " + ex.Message;
                        Console.WriteLine(msg);
                        Trace.WriteEventError(ex, msg, EventId.GenericError);
                    }

                    // Wait for it to end
                    Console.WriteLine("Service is running. Press [Enter] to close the host.");
                    Trace.WriteRegisteredMessage(EventId.ApplicationInitialized);
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteEventError(ex, "Error initializing services: " + ex.Message, EventId.ApplicationInitializationCriticalFailure);
                Console.WriteLine(ex);
                Console.WriteLine("Fatal error. Press any key to continue.");
                Console.ReadKey();
            }
            finally
            {
                Trace.WriteRegisteredMessage(VidyoEventId.ApplicationShutdown);
            }
        }
    }
}
