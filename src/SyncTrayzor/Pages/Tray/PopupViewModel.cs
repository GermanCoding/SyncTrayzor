namespace SyncTrayzor.Pages.Tray
{
    using System.Windows;

    using Stylet;

    using SyncTrayzor.Services.Config;

    public partial class PopupViewModel : Screen
    {

        private const int PopupOffsetX = -80;
        private readonly IConfigurationProvider configurationProvider;
        private bool keepOpen;

        public FileTransfersTrayViewModel FileTransfersViewModel { get; private set; }

        public PopupViewModel(IConfigurationProvider configurationProvider, FileTransfersTrayViewModel fileTransfersViewModel)
        {

            this.configurationProvider = configurationProvider;
            var configuration = configurationProvider.Load();
            configurationProvider.ConfigurationChanged += ConfigurationChanged;
            keepOpen = configuration.KeepActivityPopupOpen;

            FileTransfersViewModel = fileTransfersViewModel;
            FileTransfersViewModel.ActivateWith(this);
            FileTransfersViewModel.DeactivateWith(this);

            DisplayName = "SyncTrayzor";

            Activated += ViewModel_Activated;
            Deactivated += ViewModel_Deactivated;
        }

        private void ConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
        {
            keepOpen = e.NewConfiguration.KeepActivityPopupOpen;
            if (!keepOpen && IsActive)
            {
                RequestClose();
            }
        }

        private void ViewModel_Activated(object sender, ActivationEventArgs e)
        {
            var mousePos = WpfScreenHelper.MouseHelper.MousePosition;
            SetViewStartPosition(mousePos);
            if (View is not Window w)
            {
                return;
            }
            w.Deactivated += View_Deactivated;
        }

        private void ViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            if (View is not Window w)
            {
                return;
            }
            w.Deactivated -= View_Deactivated;
            configurationProvider.ConfigurationChanged -= ConfigurationChanged;
        }

        private void View_Deactivated(object sender, System.EventArgs e)
        {
            if (keepOpen)
            {
                return;
            }
            RequestClose();
        }

        private void SetViewStartPosition(Point popupPos)
        {
            if (View is not Window w)
            {
                return;
            }

            // get the screen the mouse is on
            var popupScreen = WpfScreenHelper.Screen.FromPoint(popupPos);
            var dpiScale = popupScreen.Bounds.Height / popupScreen.WpfBounds.Height;

            var screenWorkingArea = popupScreen.WpfWorkingArea;

            // horizontal position
            var left = (popupPos.X + PopupOffsetX) / dpiScale; // account for DPI scaling
            var right = left + w.Width;

            if (right > screenWorkingArea.Right)
            {
                left = screenWorkingArea.Right - w.Width;
            }
            w.Left = left;

            // vertical position
            var top = screenWorkingArea.Bottom - w.Height;
            w.Top = top;
        }
    }
}
