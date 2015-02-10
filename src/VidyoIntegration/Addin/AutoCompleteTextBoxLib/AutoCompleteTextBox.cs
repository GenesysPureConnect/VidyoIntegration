using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AutoCompleteTextBoxLib.Properties;
using Timer = System.Timers.Timer;

namespace AutoCompleteTextBoxLib
{
    public class AutoCompleteTextBox : TextBox, INotifyPropertyChanged
    {
        #region Private Fields

        private Guid _textChangedKey = Guid.Empty;
        private SynchronizationContext _context;
        private bool _ignoreAutoCompleteSourceCheck = false;
        private bool _isFiltering;
        private int _filterTextChangedEventDelay = 500;
        private Timer _filterTextChangedEventDelayTimer = new Timer();

        #endregion



        #region Dependency Properties

        public static readonly DependencyProperty AutoCompleteSourceProperty = DependencyProperty.Register(
            "AutoCompleteSource", typeof(IEnumerable<object>), typeof(AutoCompleteTextBox), new PropertyMetadata(default(IEnumerable<object>)));

        public static readonly DependencyProperty ComparerProperty = DependencyProperty.Register(
            "Comparer", typeof(IComparer), typeof(AutoCompleteTextBox), new PropertyMetadata(default(IComparer)));

        public static readonly DependencyProperty FilteredSourceProperty = DependencyProperty.Register(
            "FilteredSource", typeof(IEnumerable<object>), typeof(AutoCompleteTextBox), new PropertyMetadata(default(IEnumerable<object>)));

        public static readonly DependencyProperty ListMaxHeightProperty = DependencyProperty.Register(
            "ListMaxHeight", typeof (double), typeof (AutoCompleteTextBox), new PropertyMetadata(100.0));

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            "SelectedItem", typeof (object), typeof (AutoCompleteTextBox), new PropertyMetadata(default(object)));

        #endregion



        #region Public Properties

        /// <summary>
        /// When using a custom comparer, values will be considered a match when Compare(obj, text) >= 0
        /// </summary>
        public IComparer Comparer
        {
            get { return (IComparer) GetValue(ComparerProperty); }
            set { SetValue(ComparerProperty, value); }
        }

        public IEnumerable<object> AutoCompleteSource
        {
            get { return (IEnumerable<object>) GetValue(AutoCompleteSourceProperty); }
            set { SetValue(AutoCompleteSourceProperty, value); }
        }

        /// <summary>
        /// The item selected from AutoCompleteSource. When setting, the value will be ignored if it does not exist within 
        /// AutoCompleteSource and IgnoreAutoCompleteSourceCheck is set to false (default).
        /// </summary>
        //public object SelectedItem
        //{
        //    get { return _selectedThing; }
        //    set
        //    {
        //        if (value == null) return;
        //        if (!IgnoreAutoCompleteSourceCheck && AutoCompleteSource != null && !AutoCompleteSource.Contains(value)) return;

        //        _selectedThing = value;
        //        OnSelectionChanged();
        //        OnPropertyChanged();
        //    }
        //}

        public object SelectedItem
        {
            get { return (object)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public string SelectedItemDisplayText
        {
            get { return SelectedItem != null ? SelectedItem.ToString() : ""; }
        }

        public IEnumerable<object> FilteredSource
        {
            get { return (IEnumerable<object>) GetValue(FilteredSourceProperty); }
            set
            {
                SetValue(FilteredSourceProperty, value);
                OnPropertyChanged("HasFilteredItems");
            }
        }

        public bool HasFilteredItems { get { return FilteredSource != null && FilteredSource.Any(); } }

        public int FilterTextChangedEventDelay
        {
            get { return _filterTextChangedEventDelay; }
            set
            {
                _filterTextChangedEventDelay = value >= 100 ? value : 100;
                _filterTextChangedEventDelayTimer.Interval = _filterTextChangedEventDelay;
                OnPropertyChanged();
            }
        }

        public bool HasText { get { return !string.IsNullOrEmpty(Text); } }

        public bool IgnoreAutoCompleteSourceCheck
        {
            get { return _ignoreAutoCompleteSourceCheck; }
            set
            {
                _ignoreAutoCompleteSourceCheck = value;
                OnPropertyChanged();
            }
        }

        public bool IsFiltering
        {
            get { return _isFiltering; }
            set
            {
                _isFiltering = value;
                OnPropertyChanged();
            }
        }

        public double ListMaxHeight
        {
            get { return (double)GetValue(ListMaxHeightProperty); }
            set { SetValue(ListMaxHeightProperty, value); }
        }

        #endregion



        #region Eventing

        public delegate void FilterTextChangedEventHandler(FilterTextChangedEventArgs e);

        /// <summary>
        /// Raised when the text changes. This event will NOT be raised on the UI thread.
        /// </summary>
        public event FilterTextChangedEventHandler FilterTextChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion



        static AutoCompleteTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoCompleteTextBox),
                new FrameworkPropertyMetadata(typeof(AutoCompleteTextBox)));
        }



        #region Private Methods

        protected override void OnInitialized(EventArgs e)
        {
            try
            {
                // Set UI thread context
                _context = SynchronizationContext.Current;

                // Set up timer
                _filterTextChangedEventDelayTimer.Elapsed += FilterTextChangedEventDelayTimerOnElapsed;
                _filterTextChangedEventDelayTimer.AutoReset = false;
                _filterTextChangedEventDelayTimer.Interval = _filterTextChangedEventDelay;

                // Register for dependency property changed
                var dpd = DependencyPropertyDescriptor.FromProperty(SelectedItemProperty, typeof(AutoCompleteTextBox));
                if (dpd != null) dpd.AddValueChanged(this, OnSelectedItemChanged);

                // This is a fix to make the popup move with the textbox if the window changes/moves
                //TODO: this doesn't work when hosted in an ElementHost (putting WPF in WinForms)
                var window = Window.GetWindow(this);
                Console.WriteLine("window == null = " + window == null);
                if (window != null)
                {
                    window.LocationChanged += delegate
                    {
                        try
                        {
                            var myPopup = FindChild<Popup>(this, "PART_Popup");
                            if (myPopup == null) return;
                            var offset = myPopup.HorizontalOffset;
                            myPopup.HorizontalOffset = offset + 1;
                            myPopup.HorizontalOffset = offset;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            base.OnInitialized(e);
        }

        private void OnSelectedItemChanged(object sender, EventArgs eventArgs)
        {
            try
            {
                if (SelectedItem != null &&
                    !IgnoreAutoCompleteSourceCheck &&
                    (AutoCompleteSource == null || (AutoCompleteSource != null && !AutoCompleteSource.Contains(SelectedItem))))
                {
                    // Force the item to null if the source check is enabled and it fails the check
                    SelectedItem = null;
                    return;
                }

                OnSelectionChanged();
                OnPropertyChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            try
            {
                // Start on a new thread
                var t = new Thread(OnLostFocusSleeper);
                t.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            base.OnLostFocus(e);
        }

        private void OnLostFocusSleeper()
        {
            try
            {
                // This will get triggered when the user clicks away. 
                // The sleep prevents a race condition when clicking on a list item.
                Thread.Sleep(200);
                _context.Send(s => OnSelectionChanged(), null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void OnSelectionChanged()
        {
            try
            {
                // Clear the filter text
                Text = "";

                // Raise property notifications to trigger UI updates
                OnPropertyChanged("SelectedItemDisplayText");
                OnPropertyChanged("HasText");
                OnPropertyChanged("HasFilteredItems");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private T FindChild<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T) child;
                        break;
                    }
                }
                else
                {
                    // child element found.
                    foundChild = (T) child;
                    break;
                }
            }

            return foundChild;
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            try
            {
                //TODO: BUG - for some reason, the first keystroke after filtering doesn't show up in the text box, but the filter IS triggered.
                // We always handle it
                e.Handled = true;

                // Kill it on empty text
                if (string.IsNullOrEmpty(Text))
                {
                    _filterTextChangedEventDelayTimer.Stop();
                    IsFiltering = false;
                    return;
                }

                // Make it look like we're filtering
                SetFilteredSource(null);
                IsFiltering = true;

                // Restart the timer
                _filterTextChangedEventDelayTimer.Stop();
                _filterTextChangedEventDelayTimer.Start();

                // Done for now
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                // This triggers the UI to update to different states (normal, filtering, results)
                OnPropertyChanged("HasText");
            }

            base.OnTextChanged(e);
        }

        private void FilterTextChangedEventDelayTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                // Create a new guid for this event
                var newGuid = Guid.NewGuid();
                Console.WriteLine("New GUID: " + newGuid);
                _textChangedKey = newGuid;

                // Get the current text
                var text = GetText();

                // Check for empty text
                if (string.IsNullOrEmpty(text))
                {
                    CompleteDeferredFilter(null, newGuid);
                    return;
                }

                // Invoke the event for someone else to handle
                if (FilterTextChanged != null && RaiseFilterTextChanged(newGuid, text)) return;

                // Event was not invoked, we do the filtering
                FilterList(newGuid, text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                OnPropertyChanged("HasText");
            }
        }

        private bool RaiseFilterTextChanged(Guid guid, string text)
        {
            try
            {
                if (FilterTextChanged == null) return false;

                // Create event args
                var args = new FilterTextChangedEventArgs(text, this, guid);

                // Invoke event
                FilterTextChanged.DynamicInvoke(new object[] { args });

                // Is filtering deferred?
                if (args.DeferFiltering)
                {
                    // Clear the list
                    SetFilteredSource(null);

                    // Kick off filter timer
                    var t = new Thread(FilterCleanup);
                    t.Start(new Tuple<int, Guid>(args.FilterWaitTimeoutMs, guid));

                    // Done for now
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return false;
        }

        private void FilterList(Guid requestGuid, string text)
        {
            try
            {
                // Should we use the default comparison method?
                var comparer = GetComparer();
                if (comparer == null)
                {
                    FilterListUsingDefault(requestGuid, text);
                    return;
                }

                // Clear the list
                SetFilteredSource(null);
                
                // Filter the list
                var items = new List<object>();
                foreach (var item in GetAutoCompleteSource())
                {
                    // Check for a cancel
                    if (!_textChangedKey.Equals(requestGuid)) return;

                    // Compare
                    if (comparer.Compare(text, item) >= 0)
                        items.Add(item);
                }

                // Complete the filter
                CompleteDeferredFilter(items, requestGuid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void FilterListUsingDefault(Guid requestGuid, string text)
        {
            try
            {
                // Clear the list
                SetFilteredSource(null);

                // Filter the list
                var items = new List<object>();
                foreach (var item in GetAutoCompleteSource())
                {
                    // Check for a cancel
                    if (!_textChangedKey.Equals(requestGuid)) return;

                    // Compare
                    if (item.ToString().ToLower().Contains(text.ToLower().Trim()))
                        items.Add(item);
                }

                // Complete the filter
                CompleteDeferredFilter(items, requestGuid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void FilterCleanup(object data)
        {
            try
            {
                // Parse info
                var info = (Tuple<int, Guid>) data;

                // Sleep for the specified amount of time
                Thread.Sleep(info.Item1);

                // Complete the filter with a blank list for this ID
                CompleteDeferredFilter(null, info.Item2);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_context == null) return;
            _context.Send(s =>
            {
                try
                {
                    PropertyChangedEventHandler handler = PropertyChanged;
                    if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }, null);
        }

        private IEnumerable<object> GetAutoCompleteSource()
        {
            IEnumerable<object> x = null;
            _context.Send(s => x = AutoCompleteSource, null);
            return x;
        }

        private IComparer GetComparer()
        {
            IComparer x = null;
            _context.Send(s => x = Comparer, null);
            return x;
        }

        private string GetText()
        {
            string x = "";
            _context.Send(s => x = Text, null);
            return x;
        }

        private void SetFilteredSource(IEnumerable<object> list)
        {
            _context.Send(s => FilteredSource = list, null);
        }


        #endregion



        #region Public Methods

        public void CompleteDeferredFilter(IEnumerable<object> filteredSource, Guid eventId)
        {
            _context.Send(s =>
            {
                try
                {
                    Console.WriteLine(_textChangedKey + "/" + eventId);
                    // If this response wasn't from the lastest request, ignore it
                    if (_textChangedKey != eventId) return;

                    Console.WriteLine("CompleteDeferredFilter MATCH");

                    IsFiltering = false;
                    FilteredSource = filteredSource;

                    // Clear key
                    _textChangedKey = Guid.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }, null);

            // It seems that there can be a race condition with this property being set too 
            // quickly, but raising the change again here seems to make it consistently get the correct value
            OnPropertyChanged("IsFiltering");
        }

        #endregion
    }
}
