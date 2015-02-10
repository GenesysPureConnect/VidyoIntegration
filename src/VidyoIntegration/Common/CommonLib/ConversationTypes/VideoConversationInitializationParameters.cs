using System;
using System.Collections.Generic;
using ININ.IceLib.Interactions;
using VidyoIntegration.CommonLib.CicTypes;
using VidyoIntegration.CommonLib.VidyoTypes.RequestClasses;

namespace VidyoIntegration.CommonLib.ConversationTypes
{
    public abstract class VideoConversationInitializationParameters
    {
        public abstract VideoConversationMediaType MediaType { get; set; }
        public string ScopedQueueName { get; set; }
        public Dictionary<string, string> AdditionalAttributes { get; set; }
        public long InteractionId { get; set; }

        public VideoConversationInitializationParameters()
        {
            AdditionalAttributes=new Dictionary<string, string>();
        }



        public static implicit operator VideoConversationInitializationParameters(Interaction interaction)
        {
            if (interaction == null) return null;

            switch (interaction.InteractionType)
            {
                case InteractionType.Callback:
                    return interaction as CallbackInteraction;
                case InteractionType.Chat:
                    return interaction as ChatInteraction;
                case InteractionType.Generic:
                    return interaction as GenericInteraction;
                default:
                    throw new Exception("Unable to cast interaction of type " + interaction.InteractionType +
                                        " to VideoConversationInitializationParameters");
            }
        }

        public static implicit operator VideoConversationInitializationParameters(CallbackInteraction interaction)
        {
            return interaction == null
                ? null
                : new CallbackVideoConversationInitializationParameters
                {
                    InteractionId = interaction.InteractionId.Id,
                    CallbackPhoneNumber = interaction.CallbackPhone,
                    CallbackMessage = interaction.CallbackMessage
                };
        }

        public static implicit operator VideoConversationInitializationParameters(ChatInteraction interaction)
        {
            return interaction == null
                ? null
                : new ChatVideoConversationInitializationParameters
                {
                    InteractionId = interaction.InteractionId.Id,
                };
        }

        public static implicit operator VideoConversationInitializationParameters(GenericInteraction interaction)
        {
            return interaction == null
                ? null
                : new GenericInteractionVideoConversationInitializationParameters
                {
                    InteractionId = interaction.InteractionId.Id,
                    InitialState = GenericInteractionInitialState.Offering
                };
        }
    }
}