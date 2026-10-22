using SyncTrayzor.NotifyIcon;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Services.Conflicts;
using SyncTrayzor.Services.UpdateManagement;
using SyncTrayzor.Syncthing;
using System;
using System.Linq;
using SyncTrayzor.Services.Metering;
using System.Collections.Generic;
using SyncTrayzor.Services.Theming;

namespace SyncTrayzor.Services
{
    public class ConfigurationApplicator : IDisposable
    {
        private readonly IConfigurationProvider configurationProvider;

        private readonly IApplicationPathsProvider pathsProvider;
        private readonly INotifyIconManager notifyIconManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly IAutostartProvider autostartProvider;
        private readonly IWatchedFolderMonitor watchedFolderMonitor;
        private readonly IUpdateManager updateManager;
        private readonly IConflictFileWatcher conflictFileWatcher;
        private readonly IAlertsManager alertsManager;
        private readonly IMeteredNetworkManager meteredNetworkManager;
        private readonly IPathTransformer pathTransformer;
        private readonly IThemeManager themeManager;

        public ConfigurationApplicator(
            IConfigurationProvider configurationProvider,
            IApplicationPathsProvider pathsProvider,
            INotifyIconManager notifyIconManager,
            ISyncthingManager syncthingManager,
            IAutostartProvider autostartProvider,
            IWatchedFolderMonitor watchedFolderMonitor,
            IUpdateManager updateManager,
            IConflictFileWatcher conflictFileWatcher,
            IAlertsManager alertsManager,
            IMeteredNetworkManager meteredNetworkManager,
            IPathTransformer pathTransformer,
            IThemeManager themeManager)
        {
            this.configurationProvider = configurationProvider;
            this.configurationProvider.ConfigurationChanged += ConfigurationChanged;

            this.pathsProvider = pathsProvider;
            this.notifyIconManager = notifyIconManager;
            this.syncthingManager = syncthingManager;
            this.autostartProvider = autostartProvider;
            this.watchedFolderMonitor = watchedFolderMonitor;
            this.updateManager = updateManager;
            this.conflictFileWatcher = conflictFileWatcher;
            this.alertsManager = alertsManager;
            this.meteredNetworkManager = meteredNetworkManager;
            this.pathTransformer = pathTransformer;
            this.themeManager = themeManager;

            this.syncthingManager.Folders.FoldersChanged += FoldersChanged;
            this.updateManager.VersionIgnored += VersionIgnored;
        }

        private void ConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
        {
            ApplyNewConfiguration(e.NewConfiguration);
        }

        private void VersionIgnored(object sender, VersionIgnoredEventArgs e)
        {
            configurationProvider.AtomicLoadAndSave(config => config.LatestNotifiedVersion = e.IgnoredVersion);
        }

        public void ApplyConfiguration()
        {
            watchedFolderMonitor.BackoffInterval = TimeSpan.FromMilliseconds(AppSettings.Instance.DirectoryWatcherBackoffMilliseconds);
            watchedFolderMonitor.FolderExistenceCheckingInterval = TimeSpan.FromMilliseconds(AppSettings.Instance.DirectoryWatcherFolderExistenceCheckMilliseconds);

            conflictFileWatcher.BackoffInterval = TimeSpan.FromMilliseconds(AppSettings.Instance.DirectoryWatcherBackoffMilliseconds);
            conflictFileWatcher.FolderExistenceCheckingInterval = TimeSpan.FromMilliseconds(AppSettings.Instance.DirectoryWatcherFolderExistenceCheckMilliseconds);

            syncthingManager.SyncthingConnectTimeout = TimeSpan.FromSeconds(AppSettings.Instance.SyncthingConnectTimeoutSeconds);
            syncthingManager.DoSyncthingUpgrade = configurationProvider.SyncthingInstalled;

            updateManager.UpdateCheckApiUrl = AppSettings.Instance.UpdateApiUrl;

            ApplyNewConfiguration(configurationProvider.Load());
        }

        private void ApplyNewConfiguration(Configuration configuration)
        {
            notifyIconManager.MinimizeToTray = configuration.MinimizeToTray;
            notifyIconManager.CloseToTray = configuration.CloseToTray;
            notifyIconManager.ShowOnlyOnClose = configuration.ShowTrayIconOnlyOnClose;
            notifyIconManager.FolderNotificationsEnabled = configuration.Folders.ToDictionary(x => x.ID, x => x.NotificationsEnabled);
            notifyIconManager.ShowSynchronizedBalloonEvenIfNothingDownloaded = configuration.ShowSynchronizedBalloonEvenIfNothingDownloaded;
            notifyIconManager.ShowDeviceConnectivityBalloons = configuration.ShowDeviceConnectivityBalloons;
            notifyIconManager.ShowDeviceOrFolderRejectedBalloons = configuration.ShowDeviceOrFolderRejectedBalloons;

            syncthingManager.PreferredHostAndPort = configuration.SyncthingAddress;
            syncthingManager.SyncthingCommandLineFlags = configuration.SyncthingCommandLineFlags;
            syncthingManager.SyncthingEnvironmentalVariables = configuration.SyncthingEnvironmentalVariables;
            syncthingManager.SyncthingCustomHomeDir = String.IsNullOrWhiteSpace(configuration.SyncthingCustomHomePath) ?
                pathsProvider.DefaultSyncthingHomePath :
                pathTransformer.MakeAbsolute(configuration.SyncthingCustomHomePath);
            syncthingManager.SyncthingDenyUpgrade = configuration.SyncthingDenyUpgrade;
            syncthingManager.SyncthingPriorityLevel = configuration.SyncthingPriorityLevel;
            syncthingManager.SyncthingHideDeviceIds = configuration.ObfuscateDeviceIDs;
            syncthingManager.ExecutablePath = String.IsNullOrWhiteSpace(configuration.SyncthingCustomPath) ?
                pathsProvider.DefaultSyncthingPath :
                pathTransformer.MakeAbsolute(configuration.SyncthingCustomPath);

            watchedFolderMonitor.WatchedFolderIDs = configuration.Folders.Where(x => x.IsWatched).Select(x => x.ID);

            updateManager.LatestIgnoredVersion = configuration.LatestNotifiedVersion;
            updateManager.CheckForUpdates = configuration.NotifyOfNewVersions;

            conflictFileWatcher.IsEnabled = configuration.EnableConflictFileMonitoring;

            meteredNetworkManager.IsEnabled = configuration.PauseDevicesOnMeteredNetworks;

            alertsManager.EnableConflictedFileAlerts = configuration.EnableConflictFileMonitoring;
            alertsManager.EnableFailedTransferAlerts = configuration.EnableFailedTransferAlerts;

            themeManager.Apply(configuration.Theme);

            SetLogLevel(configuration);
        }

        private static readonly Dictionary<LogLevel, NLog.LogLevel> logLevelMapping = new()
        {
            { LogLevel.Info, NLog.LogLevel.Info },
            { LogLevel.Debug, NLog.LogLevel.Debug },
            { LogLevel.Trace, NLog.LogLevel.Trace },
        };

        private static void SetLogLevel(Configuration configuration)
        {
            var logLevel = logLevelMapping[configuration.LogLevel];
            var rules = NLog.LogManager.Configuration.LoggingRules;
            var logFileRule = rules.FirstOrDefault(rule => rule.Targets.Any(target => target.Name == "logfile"));
            if (logFileRule != null)
            {
                foreach (var level in NLog.LogLevel.AllLoggingLevels)
                {
                    if (level < logLevel)
                        logFileRule.DisableLoggingForLevel(level);
                    else
                        logFileRule.EnableLoggingForLevel(level);
                }
                NLog.LogManager.ReconfigExistingLoggers();
            }
        }

        private void FoldersChanged(object sender, EventArgs e)
        {
            configurationProvider.AtomicLoadAndSave(c =>
            {
                LoadFolders(c);
            });
        }

        private void LoadFolders(Configuration configuration)
        {
            var folderIds = syncthingManager.Folders.FetchAll().Select(x => x.FolderId).ToList();

            // If all folders are not watched, new folders are not watched too. Likewise notifications.
            // If there are no folders, then enable (notifications only)
            bool areAnyWatched = configuration.Folders.Any(x => x.IsWatched);
            bool areAnyNotifications = configuration.Folders.Count == 0 || configuration.Folders.Any(x => x.NotificationsEnabled);

            foreach (var newKey in folderIds.Except(configuration.Folders.Select(x => x.ID)))
            {
                configuration.Folders.Add(new FolderConfiguration(newKey, areAnyWatched, areAnyNotifications));
            }

            configuration.Folders = configuration.Folders.Where(x => folderIds.Contains(x.ID)).ToList();
        }

        public void Dispose()
        {
            configurationProvider.ConfigurationChanged -= ConfigurationChanged;
            syncthingManager.Folders.FoldersChanged -= FoldersChanged;
            updateManager.VersionIgnored -= VersionIgnored;
        }
    }
}
