namespace SyncTrayzor.Pages.Tray
{
    using System;
    using System.ComponentModel;
    using System.Windows;

    using Services.Config;

    using Stylet;

    public class PopupViewModel : Screen, IDisposable
    {
        private const int PopupOffsetX = -80;
        private readonly IConfigurationProvider configurationProvider;
        private bool keepOpen;
        private bool isClosing;
        private Point initialPopupPosition;

        public FileTransfersTrayViewModel FileTransfersViewModel { get; private set; }

        public PopupViewModel(
            IConfigurationProvider configurationProvider,
            Func<FileTransfersTrayViewModel> fileTransfersViewModelFactory
        )
        {
            initialPopupPosition = WpfScreenHelper.MouseHelper.MousePosition; // Get initial mouse position as early as possible

            this.configurationProvider = configurationProvider;
            var configuration = configurationProvider.Load();
            keepOpen = configuration.KeepActivityPopupOpen;

            FileTransfersViewModel = fileTransfersViewModelFactory();
            FileTransfersViewModel.ConductWith(this);

            DisplayName = "SyncTrayzor";

            Activated += ViewModel_Activated;
            Deactivated += ViewModel_Deactivated;
        }

        public void SetPopupPosition(Point popupPosition)
        {
            this.initialPopupPosition = popupPosition;
        }

        protected override void OnActivate()
        {
            SetViewStartPosition();
            base.OnActivate();
        }

        private void ViewModel_Activated(object sender, ActivationEventArgs e)
        {
            configurationProvider.ConfigurationChanged += ConfigurationChanged;

            if (View is not Window w)
            {
                return;
            }
            w.Deactivated += View_Deactivated;
            w.Closing += View_Closing;
        }

        private void ViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            configurationProvider.ConfigurationChanged -= ConfigurationChanged;

            if (View is not Window w)
            {
                return;
            }
            w.Deactivated -= View_Deactivated;
            w.Closing -= View_Closing;
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            isClosing = true;
        }

        private void View_Deactivated(object sender, System.EventArgs e)
        {
            if (keepOpen)
            {
                return;
            }
            if (isClosing)
            {
                return;
            }
            RequestClose();
        }

        private void ConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
        {
            keepOpen = e.NewConfiguration.KeepActivityPopupOpen;
            if (!keepOpen && IsActive)
            {
                RequestClose();
            }
        }

        private void SetViewStartPosition()
        {
            if (View is not Window w)
            {
                return;
            }

            var popupPos = initialPopupPosition;

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

        public void BringToFront()
        {
            if (View is not Window w)
            {
                return;
            }
            w.Activate();
        }

        public void Dispose()
        {
            Activated -= ViewModel_Activated;
            Deactivated -= ViewModel_Deactivated;
            FileTransfersViewModel.Dispose();
        }
    }
}
