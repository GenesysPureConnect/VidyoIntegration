using System;
using System.Windows;
using System.Windows.Controls;
using VidyoIntegration.VidyoAddin.ViewModel;

namespace VidyoIntegration.VidyoAddin.View
{
    /// <summary>
    /// Interaction logic for VidyoPanel.xaml
    /// </summary>
    public partial class VidyoPanel : UserControl
    {
        public VidyoPanel()
        {
            Console.WriteLine("Initializing VidyoPanel");
            InitializeComponent();
            Console.WriteLine("VidyoPanel Initialized");
        }

        private void AddVideoChat_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                VidyoPanelViewModel.Instance.StartVidyoChatForAgent();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }
    }
}
