using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using VidyoIntegration.TraceLib;
using Newtonsoft.Json;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.CicTypes.Serializers;
using VidyoIntegration.CommonLib.ConversationTypes;
using VidyoIntegration.CommonLib.VidyoTypes;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using VidyoIntegration.ConversationManagerLib.Supporting;

namespace VidyoIntegration.ConversationManagerLib
{
    public static class ConversationManager
    {
        #region Private Fields

        private static readonly List<VidyoConversation> Conversations = new List<VidyoConversation>();
        private static readonly object ConversationLocker = new object();
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static string ConversationFilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoIntegration", "conversations");
            }
        }

        #endregion



        #region Public Properties

        public static IReadOnlyCollection<VidyoConversation> ConversationList
        {
            get { return new ReadOnlyCollection<VidyoConversation>(Conversations); }
        }

        #endregion



        #region Constructor

        static ConversationManager()
        {
            using (Trace.Config.scope())
            {
                try
                {
                    Trace.Config.always("ConversationManager file path = {}", ConversationFilePath);

                    if (!Directory.Exists(ConversationFilePath))
                        Directory.CreateDirectory(ConversationFilePath);

                    Serializer.Converters.Add(new MediaTypeParametersJsonConverter());
                    Serializer.Converters.Add(new VideoConversationInitializationParametersJsonConverter());
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error initializing ConversationManager: "+ ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion



        #region Private Methods

        private static VidyoConversation Load(string filePath)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    // Deserialize conversation
                    Trace.Config.always("Loading conversation from {}", filePath);
                    VidyoConversation conversation;
                    using (var sr = new StreamReader(filePath))
                    using (var jsonReader = new JsonTextReader(sr))
                    {
                        conversation = Serializer.Deserialize<VidyoConversation>(jsonReader);
                    }

                    Trace.Config.always("Loaded conversation {}", conversation.ConversationId);
                    
                    return conversation;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in Load: "+ ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        private static void Delete(string conversationId)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    // Make path
                    var filePath = Path.Combine(ConversationFilePath, conversationId) + ".conversation";
                    Trace.Config.always("Attempting to delete file {}", filePath);

                    // Delete conversation
                    File.Delete(filePath);

                    Trace.Config.always("Deleted file {}", filePath);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in Delete: " + ex.Message, EventId.GenericError);
                }
            }
        }

        #endregion



        #region Public Methods

        public static void Save(VidyoConversation conversation)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    // Create path
                    var filename = Path.Combine(ConversationFilePath, conversation.ConversationId.ToString()) +
                                   ".conversation";

                    // Save file
                    Trace.Config.always("Saving conversation {} to as {}", conversation.ConversationId, filename);

                    // Lock on the filename to prevent multiple saves to the same file at the same time
                    lock (conversation.WriteLocker)
                    {
                        Trace.Config.verbose("Filename lock obtained: {}", filename);

                        // Add to list if not already there
                        if (!Conversations.Contains(conversation)) Conversations.Add(conversation);

                        // Write file
                        using (var fs = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var sw = new StreamWriter(fs))
                        using (var jw = new JsonTextWriter(sw))
                        {
                            jw.Formatting = Formatting.Indented;

                            //Serializer.Serialize(jw, conversation);
                            //var p = JsonConvert.SerializeObject(conversation.InitializationParameters, Formatting.Indented, new JsonSerializerSettings
                            //{
                            //    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                            //    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                            //});

                            var data = JsonConvert.SerializeObject(conversation, Formatting.Indented, new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                                PreserveReferencesHandling = PreserveReferencesHandling.Objects
                            });

                            jw.WriteRawValue(data);
                        }

                        Trace.Config.always("Conversation {} saved", conversation.ConversationId);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in Save: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public static void LoadConversations()
        {
            using (Trace.Config.scope())
            {
                try
                {
                    // Clear anything that's existing
                    lock (ConversationLocker)
                    {
                        Conversations.Clear();
                    }

                    // Find files to load
                    var files = Directory.GetFiles(ConversationFilePath, "*.conversation");

                    Trace.Config.note("Found {} conversations to load", files.Length);

                    // Load each file
                    foreach (var file in files)
                    {
                        // Load this one
                        var conversation = Load(file);
                        if (conversation == null) continue;

                        lock (ConversationLocker)
                        {
                            // Check for duplicate
                            var existingConversation =
                                Conversations.FirstOrDefault(c => c.ConversationId.Equals(conversation.ConversationId));
                            if (existingConversation != null)
                            {
                                Trace.Config.warning("Conversation {} already exists in list! Overwriting with loaded data.");
                                Conversations.Remove(existingConversation);
                            }

                            // Add to list
                            Conversations.Add(conversation);
                        }
                    }

                    Console.WriteLine("Loaded {0} conversations", Conversations.Count);
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in LoadConversations: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public static VidyoConversation CreateVideoConversation(VideoConversationInitializationParameters parameters)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    // Initialize the conversation object
                    var conversation = new VidyoConversation
                    {
                        ScopedQueueName = parameters.ScopedQueueName,
                        Room = VidyoServiceClient.CreateRoom(),
                        InitializationParameters = parameters
                    };

                    // Commit the conversation
                    Save(conversation);

                    return conversation;
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in CreateConversation: " + ex.Message, EventId.GenericError);
                    return null;
                }
            }
        }

        public static void RemoveConversation(VidyoConversation conversation)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    lock (ConversationLocker)
                    {
                        if (Conversations.Contains(conversation))
                        {
                            Trace.Config.note("Removing conversation {}" + conversation.ConversationId);
                            Conversations.Remove(conversation);
                            Delete(conversation.ConversationId.ToString());
                        }
                        else
                            Trace.Config.warning(
                                "Unable to remove conversation {} because it does not exist in the list.",
                                conversation.ConversationId);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteEventError(ex, "Error in RemoveConversation: " + ex.Message, EventId.GenericError);
                }
            }
        }

        public static VidyoConversation GetConversation(long interactionId)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    return Conversations.FirstOrDefault(c => c.InteractionId == interactionId);
                }
                catch (Exception ex)
                {
                    Trace.Config.exception(ex);
                    return null;
                }
            }
        }

        public static VidyoConversation GetConversation(Guid conversationId)
        {
            using (Trace.Config.scope())
            {
                try
                {
                    lock (ConversationLocker)
                    {
                        return Conversations.FirstOrDefault(c => c.ConversationId.Equals(conversationId));
                    }
                }
                catch (Exception ex)
                {
                    Trace.Config.exception(ex);
                    return null;
                }
            }
        }

        #endregion
    }
}
