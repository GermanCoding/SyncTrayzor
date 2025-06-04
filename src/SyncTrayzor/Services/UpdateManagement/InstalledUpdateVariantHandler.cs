﻿using NLog;
using System;
using System.Threading.Tasks;

namespace SyncTrayzor.Services.UpdateManagement
{
    public class InstalledUpdateVariantHandler : IUpdateVariantHandler
    {
        private const string updateDownloadFileName = "SyncTrayzorUpdate-{0}.exe";
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly IUpdateDownloader updateDownloader;
        private readonly IProcessStartProvider processStartProvider;

        private string installerPath;

        public string VariantName => "installed";
        public bool RequiresUac => true;

        public bool CanAutoInstall { get; private set; }

        public InstalledUpdateVariantHandler(
            IUpdateDownloader updateDownloader,
            IProcessStartProvider processStartProvider)
        {
            this.updateDownloader = updateDownloader;
            this.processStartProvider = processStartProvider;
        }

        public async Task<bool> TryHandleUpdateAvailableAsync(VersionCheckResults checkResult)
        {
            if (!String.IsNullOrWhiteSpace(checkResult.DownloadUrl) && !String.IsNullOrWhiteSpace(checkResult.Sha512sumDownloadUrl))
            {
                installerPath = await updateDownloader.DownloadUpdateAsync(checkResult.DownloadUrl, checkResult.Sha512sumDownloadUrl, checkResult.NewVersion, updateDownloadFileName);
                CanAutoInstall = true;

                // If we return false, the upgrade will be aborted
                return installerPath != null;
            }
            else
            {
                logger.Info($"Can't auto-install, as DownloadUrl is {checkResult.DownloadUrl} and sha512sumDownloadUrl is {checkResult.Sha512sumDownloadUrl}");
                // Can continue, but not auto-install
                CanAutoInstall = false;

                return true;
            }
        }

        public void AutoInstall(string pathToRestartApplication)
        {
            if (!CanAutoInstall)
                throw new InvalidOperationException("Auto-install not available");
            if (installerPath == null)
                throw new InvalidOperationException("TryHandleUpdateAvailableAsync returned false: cannot call AutoInstall");

            processStartProvider.StartDetached(installerPath, "/SILENT", pathToRestartApplication);
        }
    }
}
