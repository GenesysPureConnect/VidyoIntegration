using System;

namespace AutoCompleteTextBoxLib
{
    public class FilterTextChangedEventArgs : EventArgs
    {
        private int _filterWaitTimeoutMs;

        /// <summary>
        /// The new filter text
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Set to True if processing for this event has been handled
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Set to True if filtering is deferred. Use AutoCompleteTextBox.CompleteDeferredFilter(...) when filtering is complete.
        /// </summary>
        public bool DeferFiltering { get; set; }

        /// <summary>
        /// The AutoCompleteTextBox that generated the event
        /// </summary>
        public AutoCompleteTextBox Source { get; private set; }

        /// <summary>
        /// The identifier to track this event
        /// </summary>
        public Guid EventId { get; private set; }

        /// <summary>
        /// The number of miliseconds to wait for AutoCompleteTextBox.CompleteDeferredFilter(...) to be called. If the method 
        /// is not called before the timeout elapses, the filtering will be canceled and the control will display 'No matches'. 
        /// The default is 5000ms, but this value may be updated to any value >= 1000ms.
        /// </summary>
        public int FilterWaitTimeoutMs
        {
            get { return _filterWaitTimeoutMs; }
            set { _filterWaitTimeoutMs = value >= 1000 ? value : 1000; }
        }

        public FilterTextChangedEventArgs(string text, AutoCompleteTextBox source, Guid eventId)
        {
            Text = text;
            Source = source;
            EventId = eventId;
            FilterWaitTimeoutMs = 5000;
        }
    }
}
