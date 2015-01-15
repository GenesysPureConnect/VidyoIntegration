using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using VidyoIntegration.VidyoAddin.Properties;

namespace VidyoIntegration.VidyoAddin.ViewModel
{
    public class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected SynchronizationContext Context;

        public ViewModelBase()
        {
            Context = SynchronizationContext.Current;

            if (Context == null)
            {
                Trace.Main.error("Context was null for " + this.GetType(), "Context null");
                MessageBox.Show("Context was null for " + this.GetType(), "Context null");
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Context.Send(s =>
            {
                try
                {
                    PropertyChangedEventHandler handler = PropertyChanged;
                    if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
                }
                catch (Exception ex)
                {
                    
                }
            }, null);
        }

        public virtual void Dispose()
        {
            
        }
    }
}
