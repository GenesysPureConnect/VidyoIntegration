using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidyoIntegration.CommonLib.CicTypes
{
    public enum GenericInteractionInitialState
    {
        None,
        Alerting,
        Connected,
        Held,
        Messaging,
        Offering,
        Parked,
        Proceeding,
        System,
        InternalDisconnect,
        ExternalDisconnect,
        Suspended
    }
}
