using System;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.ServiceModel.Configuration;
using ININ.PSO.PsoTrace;

namespace VidyoIntegration.CommonLib
{
    public class ConfigurationProperties
    {
        // CIC
        public static string CicServer { get { return GetConfig(); } }
        public static bool CicUseWindowsAuth { get { return GetConfigBoolean(); } }
        public static string CicUsername { get { return GetConfig(); } }
        public static string CicPassword { get { return GetConfig(); } }
        public static string CicServiceEndpointUri { get { return GetConfig(); } }

        // Vidyo
        public static string VidyoAdminUsername { get { return GetConfig(); } }
        public static string VidyoAdminPassword { get { return GetConfig(); } }
        public static string VidyoWebBaseUrl { get { return GetConfig(); } }
        public static string VidyoRoomOwner { get { return GetConfig(); } }
        public static string VidyoRoomGroup { get { return GetConfig(); } }
        public static string VidyoServiceEndpointUri { get { return GetConfig(); } }
        public static ChannelEndpointElement VidyoPortalUserServicePort { get { return GetEndpointConfig(); } }
        public static ChannelEndpointElement VidyoPortalGuestServicePort { get { return GetEndpointConfig(); } }
        public static ChannelEndpointElement VidyoPortalAdminServicePort { get { return GetEndpointConfig(); } }




        private static ChannelEndpointElement GetEndpointConfig([CallerMemberName] string propertyName = "")
        {
            try
            {
                foreach (ChannelEndpointElement e in ((ClientSection)ConfigurationManager.GetSection("system.serviceModel/client")).Endpoints)
                {
                    if (e.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase))
                        return e;
                }
                return ((ClientSection)ConfigurationManager.GetSection("system.serviceModel/client")).Endpoints[propertyName];
            }
            catch (Exception ex)
            {
                Trace.WriteEventError(ex,
                    string.Format("Failed to get endpoint: {0}\n Exception: {1}", propertyName,
                        ex.Message), EventId.GenericError);
                return null;
            }
        }

        private static string GetConfig([CallerMemberName] string propertyName = "")
        {
            try
            {
                return ConfigurationManager.AppSettings.Get(propertyName);
            }
            catch (Exception ex)
            {
                Trace.WriteEventError(ex,
                    string.Format("Failed to get config parameter: {0}\n Exception: {1}", propertyName,
                        ex.Message), EventId.GenericError);
                return "";
            }
        }

        private static bool GetConfigBoolean([CallerMemberName] string propertyName = "")
        {
            try
            {
                bool val;
                bool.TryParse(ConfigurationManager.AppSettings.Get(propertyName), out val);
                return val;
            }
            catch (Exception ex)
            {
                Trace.WriteEventError(ex,
                    string.Format("Failed to get config parameter: {0}\n Exception: {1}", propertyName,
                        ex.Message), EventId.GenericError);
                return false;
            }
        }
    }
}
