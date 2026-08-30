using Stylet;
using SyncTrayzor.Pages.ConflictResolution;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Syncthing;
using System;
using System.Collections.Generic;

namespace SyncTrayzor.Pages.BarAlerts
{
    public class BarAlertsViewModel : Conductor<IBarAlert>.Collection.AllActive
    {
        private readonly IAlertsManager alertsManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly Func<ConflictResolutionViewModel> conflictResolutionViewModelFactory;
        private readonly IWindowManager windowManager;
        private readonly IConfigurationProvider configurationProvider;

        public BarAlertsViewModel(
            IAlertsManager alertsManager,
            ISyncthingManager syncthingManager,
            Func<ConflictResolutionViewModel> conflictResolutionViewModelFactory,
            IWindowManager windowManager,
            IConfigurationProvider configurationProvider)
        {
            this.alertsManager = alertsManager;
            this.syncthingManager = syncthingManager;
            this.conflictResolutionViewModelFactory = conflictResolutionViewModelFactory;
            this.windowManager = windowManager;
            this.configurationProvider = configurationProvider;
        }

        protected override void OnInitialActivate()
        {
            alertsManager.AlertsStateChanged += AlertsStateChanged;
            configurationProvider.ConfigurationChanged += AlertsStateChanged;
            Load();
        }

        private void AlertsStateChanged(object sender, EventArgs e)
        {
            Load();
        }

        private void Load()
        { 
            Items.Clear();

            var conflictedFilesCount = alertsManager.ConflictedFiles.Count;
            if (conflictedFilesCount > 0)
            {
                var vm = new ConflictsAlertViewModel(conflictedFilesCount);
                vm.OpenConflictResolverClicked += (o, e2) => OpenConflictResolver();
                Items.Add(vm);
            }

            var foldersWithFailedTransferFiles = alertsManager.FoldersWithFailedTransferFiles;
            if (foldersWithFailedTransferFiles.Count > 0)
            {
                var vm = new FailedTransfersAlertViewModel(foldersWithFailedTransferFiles);
                Items.Add(vm);
            }

            var pausedDeviceIds = alertsManager.PausedDeviceIdsFromMetering;
            if (pausedDeviceIds.Count > 0)
            {
                var pausedDeviceNames = new List<string>();
                foreach (var deviceId in pausedDeviceIds)
                {
                    if (syncthingManager.Devices.TryFetchById(deviceId, out var device))
                        pausedDeviceNames.Add(device.Name);
                }

                var vm = new PausedDevicesFromMeteringViewModel(pausedDeviceNames);
                Items.Add(vm);
            }
        }

        private void OpenConflictResolver()
        {
            var vm = conflictResolutionViewModelFactory();
            windowManager.ShowDialog(vm);
        }

        protected override void OnClose()
        {
            alertsManager.AlertsStateChanged -= AlertsStateChanged;
            configurationProvider.ConfigurationChanged -= AlertsStateChanged;
        }
    }
}
