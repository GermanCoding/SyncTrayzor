﻿using System;
using System.IO;
using NLog;

namespace SyncTrayzor.Services.Config
{
    public class ConfigFolderCleaner
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly IApplicationPathsProvider applicationPathsProvider;
        private readonly IFilesystemProvider filesystemProvider;

        public ConfigFolderCleaner(IApplicationPathsProvider applicationPathsProvider, IFilesystemProvider filesystemProvider)
        {
            this.applicationPathsProvider = applicationPathsProvider;
            this.filesystemProvider = filesystemProvider;
        }

        public void Clean()
        {
            try
            {
                CleanImpl();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to run config folder cleaner");
            }
        }

        private void CleanImpl()
        {
            // We used to have a 'logs archive' folder in the root - that's no longer used, in favour of 'logs/logs archive'
            var oldLogArchivesPath = Path.Combine(Path.GetDirectoryName(applicationPathsProvider.LogFilePath), "logs archive");
            if (filesystemProvider.DirectoryExists(oldLogArchivesPath))
            {
                logger.Info("Deleting old logs archive path: {0}", oldLogArchivesPath);
                filesystemProvider.DeleteDirectory(oldLogArchivesPath, true);
            }

            // Delete 'SyncTrayzor.log' and 'syncthing.log' in the root
            var oldSyncTrayzorRootLogPath = Path.Combine(Path.GetDirectoryName(applicationPathsProvider.ConfigurationFilePath), "SyncTrayzor.log");
            if (filesystemProvider.FileExists(oldSyncTrayzorRootLogPath))
            {
                logger.Info("Deleting old SyncTrayzor log file: {0}", oldSyncTrayzorRootLogPath);
                filesystemProvider.DeleteFile(oldSyncTrayzorRootLogPath);
            }

            var oldSyncthingRootLogPath = Path.Combine(Path.GetDirectoryName(applicationPathsProvider.ConfigurationFilePath), "syncthing.log");
            if (filesystemProvider.FileExists(oldSyncthingRootLogPath))
            {
                logger.Info("Deleting old Syncthing log file: {0}", oldSyncthingRootLogPath);
                filesystemProvider.DeleteFile(oldSyncthingRootLogPath);
            }
        }
    }
}
