using NLog;
using System;
using System.IO;

namespace SyncTrayzor.Services.Config
{
    public interface IApplicationPathsProvider
    {
        string LogFilePath { get; }
        string SyncthingBackupPath { get; }
        string ConfigurationFilePath { get; }
        string ConfigurationFileBackupPath { get; }
        string UpdatesDownloadPath { get; }
        string InstallCountFilePath { get; }
        string WebView2DataPath { get; }
        string DefaultSyncthingPath { get; }
        string DefaultSyncthingHomePath { get; }

        string UnexpandedDefaultSyncthingPath { get; }

        void Initialize(PathConfiguration pathConfiguration);
    }

    public class ApplicationPathsProvider : IApplicationPathsProvider
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly IPathTransformer pathTransformer;

        public string LogFilePath { get; private set; }
        public string SyncthingBackupPath { get; private set; }
        public string ConfigurationFilePath { get; private set; }
        public string ConfigurationFileBackupPath { get; private set; }
        public string WebView2DataPath { get; private set; }
        public string UpdatesDownloadPath { get; private set; }
        public string InstallCountFilePath { get; private set; }
        public string DefaultSyncthingPath { get; private set; }
        public string DefaultSyncthingHomePath { get; private set; }

        // Needed by migrations in the ConfigurationProvider
        public string UnexpandedDefaultSyncthingPath { get; private set; }

        public ApplicationPathsProvider(IPathTransformer pathTransformer)
        {
            this.pathTransformer = pathTransformer;
        }

        public void Initialize(PathConfiguration pathConfiguration)
        {
            if (pathConfiguration == null)
                throw new ArgumentNullException(nameof(pathConfiguration));

            LogFilePath = pathTransformer.MakeAbsolute(pathConfiguration.LogFilePath);
            SyncthingBackupPath = pathTransformer.MakeAbsolute("syncthing.exe");
            ConfigurationFilePath = pathTransformer.MakeAbsolute(pathConfiguration.ConfigurationFilePath);
            ConfigurationFileBackupPath = pathTransformer.MakeAbsolute(pathConfiguration.ConfigurationFileBackupPath);
            WebView2DataPath = pathTransformer.MakeAbsolute(pathConfiguration.WebView2DataPath);
            UpdatesDownloadPath = Path.Combine(Path.GetTempPath(), "SyncTrayzor");
            InstallCountFilePath = pathTransformer.MakeAbsolute("InstallCount.txt");
            DefaultSyncthingPath = String.IsNullOrWhiteSpace(pathConfiguration.SyncthingPath) ?
                null :
                pathTransformer.MakeAbsolute(pathConfiguration.SyncthingPath);
            DefaultSyncthingHomePath = String.IsNullOrWhiteSpace(pathConfiguration.SyncthingHomePath) ?
                null :
                pathTransformer.MakeAbsolute(pathConfiguration.SyncthingHomePath);
            UnexpandedDefaultSyncthingPath = pathConfiguration.SyncthingPath;

            logger.Debug("LogFilePath: {0}", LogFilePath);
            logger.Debug("SyncthingBackupPath: {0}", SyncthingBackupPath);
            logger.Debug("ConfigurationFilePath: {0}", ConfigurationFilePath);
            logger.Debug("ConfigurationFileBackupPath: {0}", ConfigurationFileBackupPath);
            logger.Debug("WebView2DataPath: {0}", WebView2DataPath);
            logger.Debug("DefaultSyncthingPath: {0}", DefaultSyncthingPath);
            logger.Debug("DefaultSyncthingHomePath: {0}", DefaultSyncthingHomePath);
        }
    }
}
