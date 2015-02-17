using System;
using System.Management.Instrumentation;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using ININ.IceLib.Connection;
using ININ.InteractionClient.AddIn;
using VidyoIntegration.TraceLib;
using VidyoIntegration.VidyoAddin.View;
using VidyoIntegration.VidyoAddin.ViewModel;
using MessageBox = System.Windows.Forms.MessageBox;

namespace VidyoIntegration.VidyoAddin
{
    public class VidyoAddin : AddInWindow
    {
        private ElementHost _elementHost = null;
        private VidyoPanel _vidyoPanel = null;
        private IServiceProvider _serviceProvider;
        private IInteractionSelector _interactionSelector;
        private Session _session;
        private readonly Window _window = null;

        protected override string Id
        {
            get { return "VIDYO_ADDIN"; }
        }

        protected override string DisplayName
        {
            get { return "Vidyo Addin"; }
        }

        protected override string CategoryId
        {
            get { return "VIDYO"; }
        }

        protected override string CategoryDisplayName
        {
            get { return "Vidyo Integration"; }
        }

        public override object Content
        {
            get
            {
                VidyoIntegration.VidyoAddin.Trace.Main.status("The Get_Content thread has apartment state: {}", Thread.CurrentThread.GetApartmentState());

                // Initialize view model
                VidyoPanelViewModel.Instance.Initialize(_session, _interactionSelector);

                // Initialize view
                _vidyoPanel = new VidyoPanel
                {
                    DataContext = VidyoPanelViewModel.Instance
                };
                //_vidyoPanel.InitializeComponent();
                _elementHost = new ElementHost { Dock = DockStyle.Fill, Child = _vidyoPanel };
                return _elementHost;
            }
        }
        
        public VidyoAddin()
        {
            VidyoIntegration.VidyoAddin.Trace.Initialize(typeof(EventId), "VidyoAddin");
        }

        protected override void OnLoad(IServiceProvider serviceProvider)
        {
            try
            {
                base.OnLoad(serviceProvider);

                // Store service provider reference
                _serviceProvider = serviceProvider;

                // Get Session
                _session = _serviceProvider.GetService(typeof (Session)) as Session;
                if (_session == null)
                    throw new InstanceNotFoundException("Failed to get the IceLib Session from the service provider!");

                // Get IInteractionSelector
                _interactionSelector =
                    _serviceProvider.GetService(typeof (IInteractionSelector)) as IInteractionSelector;

                VidyoIntegration.VidyoAddin.Trace.Main.status("The OnLoad thread has apartment state: {}", Thread.CurrentThread.GetApartmentState());
            }
            catch (Exception ex)
            {
                VidyoIntegration.VidyoAddin.Trace.Main.exception(ex, ex.Message);
                MessageBox.Show(
                    "Vidyo Addin initialization failed! " + ex.Message +
                    "\n\nPlease contact your system administrator.",
                    "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnUnload()
        {
            try
            {
                // Clear the vidyo panel reference
                _vidyoPanel = null;

                // Clean up the element host
                if (!_elementHost.IsDisposed)
                {
                    _elementHost.Child = null;
                    _elementHost.Dispose();
                }
                _elementHost = null;

                // Close the debug window
                if (_window != null) _window.Close();
            }
            catch (Exception ex)
            {
                VidyoIntegration.VidyoAddin.Trace.Main.exception(ex, ex.Message);
            }
            finally
            {
                base.OnUnload();
            }
        }
    }
}
