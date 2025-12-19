using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace SyncTrayzor.Services.Config
{
    [XmlRoot("Configuration")]
    public class Configuration
    {
        public const int CurrentVersion = 10;
        public const double DefaultSyncthingConsoleHeight = 100;

        [XmlAttribute("Version")]
        public int Version
        {
            get => CurrentVersion;
            set
            {
                if (CurrentVersion != value)
                    throw new InvalidOperationException($"Can't deserialize config of version {value} (expected {CurrentVersion})");
            }
        }

        public bool ShowTrayIconOnlyOnClose { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool CloseToTray { get; set; }
        public bool ShowDeviceConnectivityBalloons { get; set; }
        public bool ShowDeviceOrFolderRejectedBalloons { get; set; }
        public bool ShowSynchronizedBalloonEvenIfNothingDownloaded { get; set; }
        public bool KeepActivityPopupOpen { get; set; }
        public bool KeepActivityPopupOnTop { get; set; }
        public int ActivityPopupWidth { get; set; }
        public int ActivityPopupHeight { get; set; }
        public string SyncthingAddress { get; set; }
        public bool StartSyncthingAutomatically { get; set; }

        [XmlArrayItem("SyncthingCommandLineFlag")]
        public List<string> SyncthingCommandLineFlags { get; set; }
        public EnvironmentalVariableCollection SyncthingEnvironmentalVariables { get; set; }
        public bool SyncthingDenyUpgrade { get; set; }
        public SyncthingPriorityLevel SyncthingPriorityLevel { get; set; }

        [XmlArrayItem("Folder")]
        public List<FolderConfiguration> Folders { get; set; }

        public bool NotifyOfNewVersions { get; set; }
        public bool ObfuscateDeviceIDs { get; set; }

        [XmlIgnore]
        public Version LatestNotifiedVersion { get; set; }
        [XmlElement("LatestNotifiedVersion")]
        public string LatestNotifiedVersionRaw
        {
            get => LatestNotifiedVersion?.ToString();
            set => LatestNotifiedVersion = value == null ? null : new Version(value);
        }

        public bool UseComputerCulture { get; set; }
        public double SyncthingConsoleHeight { get; set; }
        public WindowPlacement WindowPlacement { get; set; }
        public double SyncthingWebBrowserZoomLevel { get; set; }
        public int LastSeenInstallCount { get; set; }
        public string SyncthingCustomPath { get; set; }
        public string SyncthingCustomHomePath { get; set; }
        public bool DisableHardwareRendering { get; set; }
        public bool HideIntelXeWarningMessage { get; set; }
        public bool EnableFailedTransferAlerts { get; set; }
        public bool EnableConflictFileMonitoring { get; set; }

        public bool ConflictResolverDeletesToRecycleBin { get; set; }
        public bool PauseDevicesOnMeteredNetworks { get; set; }
        public bool HaveDonated { get; set; }
        public IconAnimationMode IconAnimationMode { get; set; }
        public string OpenFolderCommand { get; set; }
        public string ShowFileInFolderCommand { get; set; }
        public LogLevel LogLevel { get; set; }

        public Configuration()
        {
            // Default configuration is for a portable setup.

            ShowTrayIconOnlyOnClose = false;
            MinimizeToTray = false;
            CloseToTray = true;
            ShowSynchronizedBalloonEvenIfNothingDownloaded = false;
            ShowDeviceConnectivityBalloons = false;
            ShowDeviceOrFolderRejectedBalloons = true;
            SyncthingAddress = "localhost:8384";
            StartSyncthingAutomatically = true;
            SyncthingCommandLineFlags = new List<string>();
            SyncthingEnvironmentalVariables = new EnvironmentalVariableCollection();
            SyncthingDenyUpgrade = false;
            SyncthingPriorityLevel = SyncthingPriorityLevel.Normal;
            Folders = new List<FolderConfiguration>();
            NotifyOfNewVersions = true;
            ObfuscateDeviceIDs = true;
            LatestNotifiedVersion = null;
            UseComputerCulture = true;
            SyncthingConsoleHeight = DefaultSyncthingConsoleHeight;
            WindowPlacement = null;
            SyncthingWebBrowserZoomLevel = 0;
            LastSeenInstallCount = 0;
            SyncthingCustomPath = null;
            SyncthingCustomHomePath = null;
            DisableHardwareRendering = false;
            HideIntelXeWarningMessage = false;
            EnableFailedTransferAlerts = true;
            EnableConflictFileMonitoring = true;
            ConflictResolverDeletesToRecycleBin = true;
            PauseDevicesOnMeteredNetworks = true;
            HaveDonated = false;
            IconAnimationMode = IconAnimationMode.DataTransferring;
            OpenFolderCommand = "explorer.exe \"{0}\"";
            ShowFileInFolderCommand = "explorer.exe /select, \"{0}\"";
            LogLevel = LogLevel.Info;
            KeepActivityPopupOpen = false;
            KeepActivityPopupOnTop = true;
            ActivityPopupWidth = 300;
            ActivityPopupHeight = 350;
        }

        public Configuration(Configuration other)
        {
            ShowTrayIconOnlyOnClose = other.ShowTrayIconOnlyOnClose;
            MinimizeToTray = other.MinimizeToTray;
            CloseToTray = other.CloseToTray;
            ShowSynchronizedBalloonEvenIfNothingDownloaded = other.ShowSynchronizedBalloonEvenIfNothingDownloaded;
            ShowDeviceConnectivityBalloons = other.ShowDeviceConnectivityBalloons;
            ShowDeviceOrFolderRejectedBalloons = other.ShowDeviceOrFolderRejectedBalloons;
            SyncthingAddress = other.SyncthingAddress;
            StartSyncthingAutomatically = other.StartSyncthingAutomatically;
            SyncthingCommandLineFlags = other.SyncthingCommandLineFlags;
            SyncthingEnvironmentalVariables = other.SyncthingEnvironmentalVariables;
            SyncthingDenyUpgrade = other.SyncthingDenyUpgrade;
            SyncthingPriorityLevel = other.SyncthingPriorityLevel;
            Folders = other.Folders.Select(x => new FolderConfiguration(x)).ToList();
            NotifyOfNewVersions = other.NotifyOfNewVersions;
            ObfuscateDeviceIDs = other.ObfuscateDeviceIDs;
            LatestNotifiedVersion = other.LatestNotifiedVersion;
            UseComputerCulture = other.UseComputerCulture;
            SyncthingConsoleHeight = other.SyncthingConsoleHeight;
            WindowPlacement = other.WindowPlacement;
            SyncthingWebBrowserZoomLevel = other.SyncthingWebBrowserZoomLevel;
            LastSeenInstallCount = other.LastSeenInstallCount;
            SyncthingCustomPath = other.SyncthingCustomPath;
            SyncthingCustomHomePath = other.SyncthingCustomHomePath;
            DisableHardwareRendering = other.DisableHardwareRendering;
            HideIntelXeWarningMessage = other.HideIntelXeWarningMessage;
            EnableFailedTransferAlerts = other.EnableFailedTransferAlerts;
            EnableConflictFileMonitoring = other.EnableConflictFileMonitoring;
            ConflictResolverDeletesToRecycleBin = other.ConflictResolverDeletesToRecycleBin;
            PauseDevicesOnMeteredNetworks = other.PauseDevicesOnMeteredNetworks;
            HaveDonated = other.HaveDonated;
            IconAnimationMode = other.IconAnimationMode;
            OpenFolderCommand = other.OpenFolderCommand;
            ShowFileInFolderCommand = other.ShowFileInFolderCommand;
            LogLevel = other.LogLevel;
            KeepActivityPopupOpen = other.KeepActivityPopupOpen;
            KeepActivityPopupOnTop = other.KeepActivityPopupOnTop;
            ActivityPopupWidth = other.ActivityPopupWidth;
            ActivityPopupHeight = other.ActivityPopupHeight;
        }

        public override string ToString()
        {
            return $"<Configuration ShowTrayIconOnlyOnClose={ShowTrayIconOnlyOnClose} MinimizeToTray={MinimizeToTray} CloseToTray={CloseToTray} " +
                $"ShowDeviceConnectivityBalloons={ShowDeviceConnectivityBalloons} ShowDeviceOrFolderRejectedBalloons={ShowDeviceOrFolderRejectedBalloons} " +
                $"SyncthingAddress={SyncthingAddress} StartSyncthingAutomatically={StartSyncthingAutomatically} " +
                $"SyncthingCommandLineFlags=[{String.Join(",", SyncthingCommandLineFlags)}] " +
                $"SyncthingEnvironmentalVariables=[{String.Join(" ", SyncthingEnvironmentalVariables)}] " +
                $"SyncthingDenyUpgrade={SyncthingDenyUpgrade} SyncthingPriorityLevel={SyncthingPriorityLevel} " +
                $"Folders=[{String.Join(", ", Folders)}] NotifyOfNewVersions={NotifyOfNewVersions} LatestNotifiedVersion={LatestNotifiedVersion} " +
                $"ObfuscateDeviceIDs={ObfuscateDeviceIDs} UseComputerCulture={UseComputerCulture} SyncthingConsoleHeight={SyncthingConsoleHeight} WindowPlacement={WindowPlacement} " +
                $"SyncthingWebBrowserZoomLevel={SyncthingWebBrowserZoomLevel} LastSeenInstallCount={LastSeenInstallCount} SyncthingCustomPath={SyncthingCustomPath} " +
                $"SyncthingCustomHomePath={SyncthingCustomHomePath} ShowSynchronizedBalloonEvenIfNothingDownloaded={ShowSynchronizedBalloonEvenIfNothingDownloaded} " +
                $"DisableHardwareRendering={DisableHardwareRendering} HideIntelXeWarningMessage={HideIntelXeWarningMessage} EnableFailedTransferAlerts={EnableFailedTransferAlerts} " +
                $"EnableConflictFileMonitoring={EnableConflictFileMonitoring} " +
                $"ConflictResolverDeletesToRecycleBin={ConflictResolverDeletesToRecycleBin} PauseDevicesOnMeteredNetworks={PauseDevicesOnMeteredNetworks} " +
                $"HaveDonated={HaveDonated} IconAnimationMode={IconAnimationMode} OpenFolderCommand={OpenFolderCommand} ShowFileInFolderCommand={ShowFileInFolderCommand}" +
                $"LogLevel={LogLevel}" +
                $"KeepActivityPopupOpen={KeepActivityPopupOpen}>" +
                $"KeepActivityPopupOnTop={KeepActivityPopupOnTop}>" +
                $"ActivityPopupWindowWidth={ActivityPopupWidth}>"+
                $"ActivityPopupWindowHeight={ActivityPopupHeight}>";
        }
    }
}
