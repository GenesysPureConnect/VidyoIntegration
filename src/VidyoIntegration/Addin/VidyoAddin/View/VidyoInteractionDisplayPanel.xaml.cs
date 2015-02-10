using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using AutoCompleteTextBoxLib;
using VidyoIntegration.CommonLib.VidyoTypes.TransportClasses;
using VidyoIntegration.VidyoAddin.Annotations;
using VidyoIntegration.VidyoAddin.ViewModel;
using VidyoIntegration.VidyoAddin.ViewModel.Helpers;

namespace VidyoIntegration.VidyoAddin.View
{
    /// <summary>
    /// Interaction logic for VidyoInteractionDisplayPanel.xaml
    /// </summary>
    public partial class VidyoInteractionDisplayPanel : UserControl, INotifyPropertyChanged
    {
        private string _guestName;
        public VidyoPanelViewModel VidyoPanelViewModel { get { return VidyoPanelViewModel.Instance; } }

        private InteractionViewModel Interaction { get { return DataContext as InteractionViewModel; } }

        public string GuestName
        {
            get { return _guestName; }
            set
            {
                _guestName = value;
                OnPropertyChanged();
                OnPropertyChanged("HasGuestName");
            }
        }

        public bool HasGuestName { get { return !string.IsNullOrEmpty(GuestName); } }


        public VidyoInteractionDisplayPanel()
        {
            InitializeComponent();
        }

        private void Conference_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                VidyoPanelViewModel.Instance.InviteToConference(Interaction, "Please join my conference");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void Transfer_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Interaction.InvokeTransfer();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void AutoCompleteTextBox_OnFilterTextChanged(FilterTextChangedEventArgs e)
        {
            try
            {
                // Update args
                e.Handled = true;
                e.DeferFiltering = true;
                e.FilterWaitTimeoutMs = 10000;

                // Call filter async method
                VidyoPanelViewModel.Instance.FilterForTransferAsync(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void MuteAudio_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var participant = (sender as Button).DataContext as Participant;
                Interaction.MuteAudio(participant, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void UnmuteAudio_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var participant = (sender as Button).DataContext as Participant;
                Interaction.MuteAudio(participant, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void MuteVideo_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var participant = (sender as Button).DataContext as Participant;
                Interaction.MuteVideo(participant, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void UnmuteVideo_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var participant = (sender as Button).DataContext as Participant;
                Interaction.MuteVideo(participant, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void KickParticipant_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var participant = (sender as Button).DataContext as Participant;
                Interaction.KickParticipant(participant);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        private void CopyLink_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(Interaction.VidyoRoomUrl + "&guestName=" + HttpUtility.UrlEncode(GuestName.Trim()));
                GuestName = "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Trace.Main.exception(ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
