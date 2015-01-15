using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace VidyoIntegration.VidyoAddin.ViewModel.Helpers
{
    public class ParticipantCollection<T> : ObservableCollection<T>
    {
        public void AddRange(IEnumerable<T> items, bool replace = false)
        {
            CheckReentrancy();

            // Optionally clear first
            if (replace) Items.Clear();

            // Privately set the items
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
