using ININ.IceLib.People;

namespace VidyoIntegration.VidyoAddin.ViewModel
{
    public class LookupEntryViewModel : ViewModelBase
    {
        public LookupEntry Entry { get; private set; }

        private LookupEntryViewModel() { }

        public override string ToString()
        {
            return Entry.DisplayName + " (" + Entry.LookupEntryType +
                   (string.IsNullOrEmpty(Entry.Extension) ? ")" : " - " + Entry.Extension + ")");
        }

        public static implicit operator LookupEntryViewModel(LookupEntry entry)
        {
            return new LookupEntryViewModel { Entry = entry };
        }
    }
}
