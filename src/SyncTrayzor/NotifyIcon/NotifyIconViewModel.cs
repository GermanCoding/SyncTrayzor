using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using Stylet;

using SyncTrayzor.Pages.Settings;
using SyncTrayzor.Pages.Tray;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.Folders;
using SyncTrayzor.Utils;

namespace SyncTrayzor.NotifyIcon
{
    public class NotifyIconViewModel : PropertyChangedBase, IDisposable
    {
        private readonly IWindowManager windowManager;
        private readonly IFocusWindowProvider focusWindowProvider;
        private readonly ISyncthingManager syncthingManager;
        private readonly Func<SettingsViewModel> settingsViewModelFactory;
        private readonly Func<PopupViewModel> popupViewModelFactory;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IAlertsManager alertsManager;
        private readonly IConfigurationProvider configurationProvider;

        public bool Visible { get; set; }
        public bool MainWindowVisible { get; set; }
        public BindableCollection<FolderViewModel> Folders { get; private set; }
        public FileTransfersTrayViewModel FileTransfersViewModel { get; private set; }

        public event EventHandler WindowOpenRequested;
        public event EventHandler WindowCloseRequested;
        public event EventHandler ExitRequested;

        public SyncthingState SyncthingState { get; set; }

        public bool SyncthingDevicesPaused => alertsManager.PausedDeviceIdsFromMetering.Count > 0;

        public bool SyncthingWarning => alertsManager.AnyWarnings;

        public bool SyncthingStarted => SyncthingState == SyncthingState.Running;

        public bool SyncthingSyncing { get; private set; }

        private IconAnimationMode iconAnimationmode;

        Point popupPosition;
        PopupViewModel popupViewModel;

        public NotifyIconViewModel(
            IWindowManager windowManager,
            IFocusWindowProvider focusWindowProvider,
            ISyncthingManager syncthingManager,
            Func<SettingsViewModel> settingsViewModelFactory,
            Func<PopupViewModel> popupViewModelFactory,
            IProcessStartProvider processStartProvider,
            IAlertsManager alertsManager,
            FileTransfersTrayViewModel fileTransfersViewModel,
            IConfigurationProvider configurationProvider)
        {
            this.windowManager = windowManager;
            this.focusWindowProvider = focusWindowProvider;
            this.syncthingManager = syncthingManager;
            this.settingsViewModelFactory = settingsViewModelFactory;
            this.popupViewModelFactory = popupViewModelFactory;
            this.processStartProvider = processStartProvider;
            this.alertsManager = alertsManager;
            FileTransfersViewModel = fileTransfersViewModel;
            this.configurationProvider = configurationProvider;

            this.syncthingManager.StateChanged += StateChanged;
            SyncthingState = this.syncthingManager.State;

            this.syncthingManager.TotalConnectionStatsChanged += TotalConnectionStatsChanged;
            this.syncthingManager.Folders.FoldersChanged += FoldersChanged;
            this.syncthingManager.Folders.SyncStateChanged += FolderSyncStateChanged;


            this.alertsManager.AlertsStateChanged += AlertsStateChanged;

            this.configurationProvider.ConfigurationChanged += ConfigurationChanged;
            var configuration = this.configurationProvider.Load();
            iconAnimationmode = configuration.IconAnimationMode;
        }

        private void StateChanged(object sender, SyncthingStateChangedEventArgs e)
        {
            SyncthingState = e.NewState;
            if (e.NewState != SyncthingState.Running)
                SyncthingSyncing = false; // Just make sure we reset this..
        }

        private void TotalConnectionStatsChanged(object sender, ConnectionStatsChangedEventArgs e)
        {
            if (iconAnimationmode == IconAnimationMode.DataTransferring)
            {
                var stats = e.TotalConnectionStats;
                SyncthingSyncing = stats.InBytesPerSecond > 0 || stats.OutBytesPerSecond > 0;
            }
        }

        private void FoldersChanged(object sender, EventArgs e)
        {
            Folders = new BindableCollection<FolderViewModel>(syncthingManager.Folders.FetchAll()
                    .Select(x => new FolderViewModel(x, processStartProvider))
                    .OrderBy(x => x.FolderLabel));
        }

        private void FolderSyncStateChanged(object sender, FolderSyncStateChangedEventArgs e)
        {
            if (iconAnimationmode == IconAnimationMode.Syncing)
            {
                var anySyncing = syncthingManager.Folders.FetchAll().Any(x => x.SyncState == FolderSyncState.Syncing);
                SyncthingSyncing = anySyncing;
            }
        }

        private void AlertsStateChanged(object sender, EventArgs e)
        {
            NotifyOfPropertyChange(nameof(SyncthingDevicesPaused));
            NotifyOfPropertyChange(nameof(SyncthingWarning));
        }

        private void ConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
        {
            iconAnimationmode = e.NewConfiguration.IconAnimationMode;
            // Reset, just in case
            SyncthingSyncing = false;
        }

        public void TrayLeftMouseDown()
        {
            // Capture mouse position incase we are about to show the popup window
            popupPosition = WpfScreenHelper.MouseHelper.MousePosition;
            popupViewModel?.BringToFront();
        }

        public void Click()
        {
            if (popupViewModel != null)
            {
                return;
            }
            popupViewModel = popupViewModelFactory();
            popupViewModel.Closed += (s, e) => popupViewModel = null;
            popupViewModel.SetPopupPosition(popupPosition);
            windowManager.ShowWindow(popupViewModel);
            popupViewModel.BringToFront();
        }

        public void DoubleClick()
        {
            OnWindowOpenRequested();
        }

        public void ShowSettings()
        {
            if (!focusWindowProvider.TryFocus<SettingsViewModel>())
            {
                var vm = settingsViewModelFactory();
                windowManager.ShowDialog(vm);
            }
        }

        public void Restore()
        {
            OnWindowOpenRequested();
        }

        public void Minimize()
        {
            OnWindowCloseRequested();
        }

        public bool CanStart => SyncthingState == SyncthingState.Stopped;
        public async void Start()
        {
            await syncthingManager.StartWithErrorDialogAsync(windowManager);
        }

        public bool CanStop => SyncthingState == SyncthingState.Running;
        public async void Stop()
        {
            await syncthingManager.StopAsync();
        }

        public bool CanRestart => SyncthingState == SyncthingState.Running;
        public async void Restart()
        {
            await syncthingManager.RestartAsync();
        }

        public bool CanRescanAll => SyncthingState == SyncthingState.Running;
        public async void RescanAll()
        {
            await syncthingManager.ScanAsync(null, null);
        }

        public void Exit()
        {
            OnExitRequested();
        }

        private void OnWindowOpenRequested() => WindowOpenRequested?.Invoke(this, EventArgs.Empty);

        private void OnWindowCloseRequested() => WindowCloseRequested?.Invoke(this, EventArgs.Empty);

        private void OnExitRequested() => ExitRequested?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            syncthingManager.StateChanged -= StateChanged;

            syncthingManager.TotalConnectionStatsChanged -= TotalConnectionStatsChanged;
            syncthingManager.Folders.SyncStateChanged -= FolderSyncStateChanged;
            syncthingManager.Folders.FoldersChanged -= FoldersChanged;

            alertsManager.AlertsStateChanged -= AlertsStateChanged;

            configurationProvider.ConfigurationChanged -= ConfigurationChanged;

            popupViewModel?.Dispose();
        }
    }

    // Slightly hacky, as we can't use s:Action in a style setter...
    public class FolderViewModel : ICommand
    {
        private readonly Folder folder;
        private readonly IProcessStartProvider processStartProvider;

        public string FolderLabel => folder.Label;

        public FolderViewModel(Folder folder, IProcessStartProvider processStartProvider)
        {
            this.folder = folder;
            this.processStartProvider = processStartProvider;
        }

        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            processStartProvider.ShowFolderInExplorer(folder.Path);
        }
    }
}
