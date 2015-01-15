using ININ.IceLib.Interactions;

namespace VidyoIntegration.CommonLib.CicTypes.RequestClasses
{
    public class GenericInteractionMediaTypeParameters : MediaTypeParameters
    {
        private GenericInteractionInitialState _initialState;

        public GenericInteractionInitialState InitialState
        {
            get { return _initialState; }
            set { _initialState = value; }
        }

        public override VideoConversationMediaType MediaType
        {
            get { return VideoConversationMediaType.GenericInteraction; }
        }
    }
}