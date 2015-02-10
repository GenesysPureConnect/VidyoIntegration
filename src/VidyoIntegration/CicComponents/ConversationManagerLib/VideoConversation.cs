using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.CicTypes.TransportClasses;
using VidyoIntegration.CommonLib.ConversationTypes;
using VidyoIntegration.CommonLib.VidyoTypes;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using VidyoIntegration.ConversationManagerLib.Supporting;

namespace VidyoIntegration.ConversationManagerLib
{
    public class VidyoConversation
    {
        #region Private Fields

        private readonly object _attributeUpdateLocker = new object();

        internal object WriteLocker = new object();
        private Room _room;

        #endregion



        #region Public Properties

        /// <summary>
        /// GUID for this conversation
        /// </summary>
        public Guid ConversationId { get; set; }

        /// <summary>
        /// The interaction ID associated with this conversation
        /// </summary>
        public long InteractionId { get; set; }

        public Room Room
        {
            get { return _room; }
            set
            {
                _room = value;
                RoomUrl = MakeRoomUrl(value);
            }
        }

        public Dictionary<string, string> AttributeDictionary { get; set; }

        public string ScopedQueueName { get; set; }

        public bool IsConversationMuted { get; set; }

        public VideoConversationInitializationParameters InitializationParameters { get; set; }

        /// <summary>
        /// Will have the CIC username of the owner if the interaction is assigned. If it 
        /// is not assigned to a user (still ACD wait), will be empty.
        /// </summary>
        public string UserOwner { get; set; }

        public string RoomUrl { get; set; }

        #endregion



        #region Constructor

        internal VidyoConversation()
        {
            ConversationId = Guid.NewGuid();
            InteractionId = 0;
            AttributeDictionary = new Dictionary<string, string>();
            ScopedQueueName = "";
            IsConversationMuted = false;
            RoomUrl = "";
        }

        #endregion



        #region Private Methods

        private static string MakeRoomUrl(Room room)
        {
            var url = ConfigurationProperties.VidyoWebBaseUrl +
                      "?portalUri=" + HttpUtility.UrlEncode(room.RoomUrl) +
                      "&key=" + HttpUtility.UrlEncode(room.RoomKey) +
                      "&roomPin=" + HttpUtility.UrlEncode(room.Pin);
            return url;
        }

        #endregion



        #region Public Methods

        public void UpdateAttributes(IDictionary<string, string> attributeDictionary)
        {
            using (Trace.Main.scope())
            {
                try
                {
                    // Only process one update request at a time
                    lock (_attributeUpdateLocker)
                    {
                        // Update dictionary
                        foreach (var kvp in attributeDictionary)
                        {
                            AttributeDictionary[kvp.Key] = kvp.Value;
                        }

                        // Save this
                        Save();
                    }
                }
                catch (Exception ex)
                {
                    Trace.Main.exception(ex);
                }
            }
        }

        public void Save()
        {
            ConversationManager.Save(this);
        }

        #endregion
    }
}
