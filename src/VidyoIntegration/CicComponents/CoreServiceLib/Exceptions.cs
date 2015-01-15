using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidyoIntegration.CoreServiceLib
{
    public class ConversationReconstitutionException : Exception
    {
        public ConversationReconstitutionException(string reason) : base(reason)
        {
        }
    }
}
