using FluentValidation;
using Stylet;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.IO;
using SyncTrayzor.Services.Metering;

namespace SyncTrayzor.Pages.Settings
{
    public class FolderSettings : PropertyChangedBase
    {
        public string FolderId { get; set; }
        public string FolderLabel { get; set; }
        public bool IsWatched { get; set; }
        public bool IsWatchAllowed { get; set; }
        public bool VisibleIsWatched
        {
            get => IsWatched && IsWatchAllowed;
            set
            {
                if (IsWatchAllowed)
                    IsWatched = value;
                else
                    throw new InvalidOperationException();
            }
        }

        public bool IsNotified { get; set; }
    }

    public class SettingsViewModel : Screen
    {
        // We can be opened directly on this tab. All of the layout is done in xaml, so this is
        // the neatest way we can select it...
        private const int loggingTabIndex = 3;

        private readonly IConfigurationProvider configurationProvider;
        private readonly IAutostartProvider autostartProvider;
        private readonly IWindowManager windowManager;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IAssemblyProvider assemblyProvider;
        private readonly IApplicationState applicationState;
        private readonly IApplicationPathsProvider applicationPathsProvider;
        private readonly ISyncthingManager syncthingManager;
        private readonly List<SettingItem> settings = new();

        public int SelectedTabIndex { get; set; }

        public SettingItem<bool> MinimizeToTray { get; }
        public SettingItem<bool> CloseToTray { get; }
        public SettingItem<bool> NotifyOfNewVersions { get; }
        public SettingItem<bool> ObfuscateDeviceIDs { get; }
        public SettingItem<bool> UseComputerCulture { get; }
        public SettingItem<bool> DisableHardwareRendering { get; }
        public SettingItem<bool> EnableConflictFileMonitoring { get; }
        public SettingItem<bool> EnableFailedTransferAlerts { get; }

        public bool PauseDevicesOnMeteredNetworksSupported { get; }
        public SettingItem<bool> PauseDevicesOnMeteredNetworks { get; }

        public SettingItem<bool> ShowTrayIconOnlyOnClose { get; }
        public SettingItem<bool> ShowSynchronizedBalloonEvenIfNothingDownloaded { get; }
        public SettingItem<bool> ShowDeviceConnectivityBalloons { get; }
        public SettingItem<bool> ShowDeviceOrFolderRejectedBalloons { get; }
        public SettingItem<bool> KeepActivityPopupOpen { get; }
        public SettingItem<bool> KeepActivityPopupOnTop { get; }
        public SettingItem<int> ActivityPopupWidth { get; }
        public SettingItem<int> ActivityPopupHeight { get; }

        public BindableCollection<LabelledValue<IconAnimationMode>> IconAnimationModes { get; }
        public SettingItem<IconAnimationMode> IconAnimationMode { get; }

        public SettingItem<bool> StartSyncthingAutomatically { get; }

        public BindableCollection<LabelledValue<SyncthingPriorityLevel>> PriorityLevels { get; }
        public SettingItem<SyncthingPriorityLevel> SyncthingPriorityLevel { get; }

        public SettingItem<string> SyncthingAddress { get; }

        public bool CanReadAutostart { get; set; }
        public bool CanWriteAutostart { get; set; }
        public bool CanReadOrWriteAutostart => CanReadAutostart || CanWriteAutostart; 
        public bool CanReadAndWriteAutostart => CanReadAutostart && CanWriteAutostart;
        public bool StartOnLogon { get; set; }
        public bool StartMinimized { get; set; }
        public bool StartMinimizedEnabled => CanReadAndWriteAutostart && StartOnLogon;
        public SettingItem<string> SyncthingCommandLineFlags { get; }
        public SettingItem<string> SyncthingEnvironmentalVariables { get; }
        public SettingItem<string> SyncthingCustomPath { get; }
        public SettingItem<string> SyncthingCustomHomePath { get; }
        public SettingItem<bool> SyncthingDenyUpgrade { get;  }

        private bool updatingFolderSettings;
        public bool? AreAllFoldersWatched { get; set; }
        public bool? AreAllFoldersNotified { get; set; }
        public BindableCollection<FolderSettings> FolderSettings { get; } = new();
        public bool IsAnyFolderWatchEnabledInSyncthing { get; private set; }

        public BindableCollection<LabelledValue<LogLevel>> LogLevels { get; }
        public SettingItem<LogLevel> SelectedLogLevel { get; set; }

        public SettingsViewModel(
            IConfigurationProvider configurationProvider,
            IAutostartProvider autostartProvider,
            IWindowManager windowManager,
            IProcessStartProvider processStartProvider,
            IAssemblyProvider assemblyProvider,
            IApplicationState applicationState,
            IApplicationPathsProvider applicationPathsProvider,
            ISyncthingManager syncthingManager,
            IMeteredNetworkManager meteredNetworkManager)
        {
            this.configurationProvider = configurationProvider;
            this.autostartProvider = autostartProvider;
            this.windowManager = windowManager;
            this.processStartProvider = processStartProvider;
            this.assemblyProvider = assemblyProvider;
            this.applicationState = applicationState;
            this.applicationPathsProvider = applicationPathsProvider;
            this.syncthingManager = syncthingManager;

            MinimizeToTray = CreateBasicSettingItem(x => x.MinimizeToTray);
            NotifyOfNewVersions = CreateBasicSettingItem(x => x.NotifyOfNewVersions);
            CloseToTray = CreateBasicSettingItem(x => x.CloseToTray);
            ObfuscateDeviceIDs = CreateBasicSettingItem(x => x.ObfuscateDeviceIDs);
            UseComputerCulture = CreateBasicSettingItem(x => x.UseComputerCulture);
            UseComputerCulture.RequiresSyncTrayzorRestart = true;
            DisableHardwareRendering = CreateBasicSettingItem(x => x.DisableHardwareRendering);
            DisableHardwareRendering.RequiresSyncTrayzorRestart = true;
            EnableConflictFileMonitoring = CreateBasicSettingItem(x => x.EnableConflictFileMonitoring);
            EnableFailedTransferAlerts = CreateBasicSettingItem(x => x.EnableFailedTransferAlerts);

            PauseDevicesOnMeteredNetworks = CreateBasicSettingItem(x => x.PauseDevicesOnMeteredNetworks);
            PauseDevicesOnMeteredNetworksSupported = meteredNetworkManager.IsSupportedByWindows;

            ShowTrayIconOnlyOnClose = CreateBasicSettingItem(x => x.ShowTrayIconOnlyOnClose);
            ShowSynchronizedBalloonEvenIfNothingDownloaded = CreateBasicSettingItem(x => x.ShowSynchronizedBalloonEvenIfNothingDownloaded);
            ShowDeviceConnectivityBalloons = CreateBasicSettingItem(x => x.ShowDeviceConnectivityBalloons);
            ShowDeviceOrFolderRejectedBalloons = CreateBasicSettingItem(x => x.ShowDeviceOrFolderRejectedBalloons);
            KeepActivityPopupOpen = CreateBasicSettingItem(x => x.KeepActivityPopupOpen);
            KeepActivityPopupOnTop = CreateBasicSettingItem(x => x.KeepActivityPopupOnTop);
            ActivityPopupWidth = CreateBasicSettingItem(x => x.ActivityPopupWidth);
            ActivityPopupHeight = CreateBasicSettingItem(x => x.ActivityPopupHeight );

            IconAnimationModes = new BindableCollection<LabelledValue<IconAnimationMode>>()
            {
                LabelledValue.Create(Resources.SettingsView_TrayIconAnimation_DataTransferring, Services.Config.IconAnimationMode.DataTransferring),
                LabelledValue.Create(Resources.SettingsView_TrayIconAnimation_Syncing, Services.Config.IconAnimationMode.Syncing),
                LabelledValue.Create(Resources.SettingsView_TrayIconAnimation_Disabled, Services.Config.IconAnimationMode.Disabled),
            };
            IconAnimationMode = CreateBasicSettingItem(x => x.IconAnimationMode);

            StartSyncthingAutomatically = CreateBasicSettingItem(x => x.StartSyncthingAutomatically);
            SyncthingPriorityLevel = CreateBasicSettingItem(x => x.SyncthingPriorityLevel);
            SyncthingPriorityLevel.RequiresSyncthingRestart = true;
            SyncthingAddress = CreateBasicSettingItem(x => x.SyncthingAddress, new SyncthingAddressValidator());
            SyncthingAddress.RequiresSyncthingRestart = true;

            CanReadAutostart = this.autostartProvider.CanRead;
            CanWriteAutostart = this.autostartProvider.CanWrite;
            if (this.autostartProvider.CanRead)
            {
                var currentSetup = this.autostartProvider.GetCurrentSetup();
                StartOnLogon = currentSetup.AutoStart;
                StartMinimized = currentSetup.StartMinimized;
            }

            SyncthingCommandLineFlags = CreateBasicSettingItem(
                x => String.Join(" ", x.SyncthingCommandLineFlags),
                (x, v) =>
                {
                    KeyValueStringParser.TryParse(v, out var envVars, mustHaveValue: false);
                    x.SyncthingCommandLineFlags = envVars.Select(item => KeyValueStringParser.FormatItem(item.Key, item.Value)).ToList();
                }, new SyncthingCommandLineFlagsValidator());
            SyncthingCommandLineFlags.RequiresSyncthingRestart = true;

            SyncthingEnvironmentalVariables = CreateBasicSettingItem(
                x => KeyValueStringParser.Format(x.SyncthingEnvironmentalVariables),
                (x, v) =>
                {
                    KeyValueStringParser.TryParse(v, out var envVars);
                    x.SyncthingEnvironmentalVariables = new EnvironmentalVariableCollection(envVars);
                }, new SyncthingEnvironmentalVariablesValidator());
            SyncthingEnvironmentalVariables.RequiresSyncthingRestart = true;


            SyncthingCustomPath = CreateBasicSettingItem(x => x.SyncthingCustomPath);
            // This *shouldn't* be necessary, but the code to copy the syncthing.exe binary if it doesn't exist
            // is only run at startup, so require a restart...
            SyncthingCustomPath.RequiresSyncTrayzorRestart = true;

            SyncthingCustomHomePath = CreateBasicSettingItem(x => x.SyncthingCustomHomePath);
            SyncthingCustomHomePath.RequiresSyncthingRestart = true;

            SyncthingDenyUpgrade = CreateBasicSettingItem(x => x.SyncthingDenyUpgrade);
            SyncthingDenyUpgrade.RequiresSyncthingRestart = true;

            PriorityLevels = new BindableCollection<LabelledValue<SyncthingPriorityLevel>>()
            {
                LabelledValue.Create(Resources.SettingsView_Syncthing_ProcessPriority_AboveNormal, Services.Config.SyncthingPriorityLevel.AboveNormal),
                LabelledValue.Create(Resources.SettingsView_Syncthing_ProcessPriority_Normal, Services.Config.SyncthingPriorityLevel.Normal),
                LabelledValue.Create(Resources.SettingsView_Syncthing_ProcessPriority_BelowNormal, Services.Config.SyncthingPriorityLevel.BelowNormal),
                LabelledValue.Create(Resources.SettingsView_Syncthing_ProcessPriority_Idle, Services.Config.SyncthingPriorityLevel.Idle),
            };

            LogLevels = new BindableCollection<LabelledValue<LogLevel>>()
            {
                LabelledValue.Create(Resources.SettingsView_Logging_LogLevel_Info, LogLevel.Info),
                LabelledValue.Create(Resources.SettingsView_Logging_LogLevel_Debug, LogLevel.Debug),
                LabelledValue.Create(Resources.SettingsView_Logging_LogLevel_Trace, LogLevel.Trace),
            };
            SelectedLogLevel = CreateBasicSettingItem(x => x.LogLevel);

            var configuration = this.configurationProvider.Load();

            foreach (var settingItem in settings)
            {
                settingItem.LoadValue(configuration);
            }

            this.Bind(s => s.FolderSettings, (o2, e2) =>
            {
                foreach (var folderSetting in FolderSettings)
                {
                    folderSetting.Bind(s => s.IsWatched, (o, e) => UpdateAreAllFoldersWatched());
                    folderSetting.Bind(s => s.IsNotified, (o, e) => UpdateAreAllFoldersNotified());
                }
            });

            this.Bind(s => s.AreAllFoldersNotified, (o, e) =>
            {
                if (updatingFolderSettings)
                    return;

                updatingFolderSettings = true;

                foreach (var folderSetting in FolderSettings)
                {
                    folderSetting.IsNotified = e.NewValue.GetValueOrDefault(false);
                }

                updatingFolderSettings = false;
            });

            this.Bind(s => s.AreAllFoldersWatched, (o, e) =>
            {
                if (updatingFolderSettings)
                    return;

                updatingFolderSettings = true;

                foreach (var folderSetting in FolderSettings)
                {
                    if (folderSetting.IsWatchAllowed)
                        folderSetting.IsWatched = e.NewValue.GetValueOrDefault(false);
                }

                updatingFolderSettings = false;
            });

            UpdateAreAllFoldersWatched();
            UpdateAreAllFoldersNotified();
        }

        protected override void OnInitialActivate()
        {
            if (syncthingManager.State == SyncthingState.Running && syncthingManager.IsDataLoaded)
                LoadFromSyncthingStartupData();
            else
                syncthingManager.DataLoaded += SyncthingDataLoaded;
        }

        protected override void OnClose()
        {
            syncthingManager.DataLoaded -= SyncthingDataLoaded;
        }

        private void SyncthingDataLoaded(object sender, EventArgs e)
        {
            LoadFromSyncthingStartupData();
        }

        private void LoadFromSyncthingStartupData()
        {
            var configuration = configurationProvider.Load();

            // We have to merge two sources of data: the folder settings from config, and the actual folder
            // configuration from Syncthing (which we use to get the folder label). They should be in sync...

            FolderSettings.Clear();

            var folderSettings = new List<FolderSettings>(configuration.Folders.Count);

            foreach (var configFolder in configuration.Folders)
            {
                if (syncthingManager.Folders.TryFetchById(configFolder.ID, out var folder))
                {
                    folderSettings.Add(new FolderSettings()
                    {
                        FolderId = configFolder.ID,
                        FolderLabel = folder?.Label ?? configFolder.ID,
                        IsWatched = configFolder.IsWatched,
                        IsWatchAllowed = !folder.IsFsWatcherEnabled,
                        IsNotified = configFolder.NotificationsEnabled,
                    });
                }
            }

            FolderSettings.AddRange(folderSettings.OrderBy(x => x.FolderLabel));

            IsAnyFolderWatchEnabledInSyncthing = FolderSettings.Any(x => !x.IsWatchAllowed);

            UpdateAreAllFoldersWatched();
            UpdateAreAllFoldersNotified();

            NotifyOfPropertyChange(nameof(FolderSettings));
        }

        private SettingItem<T> CreateBasicSettingItem<T>(Expression<Func<Configuration, T>> accessExpression, IValidator<SettingItem<T>> validator = null)
        {
            return CreateBasicSettingItemImpl(v => new SettingItem<T>(accessExpression, v), validator);
        }

        private SettingItem<T> CreateBasicSettingItem<T>(Func<Configuration, T> getter, Action<Configuration, T> setter, IValidator<SettingItem<T>> validator = null, Func<T, T, bool> comparer = null)
        {
            return CreateBasicSettingItemImpl(v => new SettingItem<T>(getter, setter, v, comparer), validator);
        }

        private SettingItem<T> CreateBasicSettingItemImpl<T>(Func<IModelValidator, SettingItem<T>> generator, IValidator<SettingItem<T>> validator)
        {
            IModelValidator modelValidator = validator == null ? null : new FluentModelValidator<SettingItem<T>>(validator);
            var settingItem = generator(modelValidator);
            settings.Add(settingItem);
            settingItem.ErrorsChanged += (o, e) => NotifyOfPropertyChange(() => CanSave);
            return settingItem;
        }

        private void UpdateAreAllFoldersWatched()
        {
            if (updatingFolderSettings)
                return;

            updatingFolderSettings = true;

            if (FolderSettings.All(x => x.VisibleIsWatched))
                AreAllFoldersWatched = true;
            else if (FolderSettings.All(x => !x.VisibleIsWatched))
                AreAllFoldersWatched = false;
            else
                AreAllFoldersWatched = null;

            updatingFolderSettings = false;
        }

        private void UpdateAreAllFoldersNotified()
        {
            if (updatingFolderSettings)
                return;

            updatingFolderSettings = true;

            if (FolderSettings.All(x => x.IsNotified))
                AreAllFoldersNotified = true;
            else if (FolderSettings.All(x => !x.IsNotified))
                AreAllFoldersNotified = false;
            else
                AreAllFoldersNotified = null;

            updatingFolderSettings = false;
        }

        public bool CanSave => settings.All(x => !x.HasErrors);
        public void Save()
        {
            configurationProvider.AtomicLoadAndSave(configuration =>
            {
                foreach (var settingItem in settings)
                {
                    settingItem.SaveValue(configuration);
                }

                configuration.Folders = FolderSettings.Select(x => new FolderConfiguration(x.FolderId, x.IsWatched, x.IsNotified)).ToList();
            });

            if (autostartProvider.CanWrite)
            {
                // I've seen this fail, even though we successfully wrote on startup
                try
                {
                    var autostartConfig = new AutostartConfiguration() { AutoStart = StartOnLogon, StartMinimized = StartMinimized };
                    autostartProvider.SetAutoStart(autostartConfig);
                }
                catch
                {
                    windowManager.ShowMessageBox(
                        Resources.SettingsView_CannotSetAutoStart_Message,
                        Resources.SettingsView_CannotSetAutoStart_Title,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (settings.Any(x => x.HasChanged && x.RequiresSyncTrayzorRestart))
            {
                var result = windowManager.ShowMessageBox(
                    Resources.SettingsView_RestartSyncTrayzor_Message,
                    Resources.SettingsView_RestartSyncTrayzor_Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    processStartProvider.StartDetached(Environment.ProcessPath!);
                    applicationState.Shutdown();
                }
            }
            else if ((settings.Any(x => x.HasChanged && x.RequiresSyncthingRestart)) &&
                syncthingManager.State == SyncthingState.Running)
            {
                var result = windowManager.ShowMessageBox(
                    Resources.SettingsView_RestartSyncthing_Message,
                    Resources.SettingsView_RestartSyncthing_Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    RestartSyncthing();
                }
            }

            RequestClose(true);
        }

        private async void RestartSyncthing()
        {
            await syncthingManager.RestartAsync();
        }

        public void Cancel()
        {
            RequestClose(false);
        }

        public void ShowSyncthingLogFile()
        {
            processStartProvider.ShowFileInExplorer(Path.Combine(applicationPathsProvider.LogFilePath, "syncthing.log"));
        }

        public void ShowSyncTrayzorLogFile()
        {
            processStartProvider.ShowFileInExplorer(Path.Combine(applicationPathsProvider.LogFilePath, "SyncTrayzor.log"));
        }

        public void SelectLoggingTab()
        {
            SelectedTabIndex = loggingTabIndex;
        }
    }
}
