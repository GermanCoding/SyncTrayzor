using Stylet;
using SyncTrayzor.Services;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.TransferHistory;
using SyncTrayzor.Utils;
using System;
using System.IO;
using System.Linq;

namespace SyncTrayzor.Pages.Tray
{
    public class FileTransfersTrayViewModel : Screen, IDisposable
    {
        // Same as the queue limit in SyncthingTransferHistory
        private const int CompletedTransfersToDisplay = 100;

        private readonly ISyncthingManager syncthingManager;
        private readonly IProcessStartProvider processStartProvider;

        public NetworkGraphViewModel NetworkGraph { get; }

        public BindableCollection<FileTransferViewModel> CompletedTransfers { get; private set; }
        public BindableCollection<FileTransferViewModel> InProgressTransfers { get; private set; }

        public bool HasCompletedTransfers => CompletedTransfers.Count > 0;
        public bool HasInProgressTransfers => InProgressTransfers.Count > 0;

        public string InConnectionRate { get; private set; }
        public string OutConnectionRate { get; private set; }

        public bool AnyTransfers => HasCompletedTransfers || HasInProgressTransfers;

        public FileTransfersTrayViewModel(ISyncthingManager syncthingManager,
            IProcessStartProvider processStartProvider, NetworkGraphViewModel networkGraph)
        {
            this.syncthingManager = syncthingManager;
            this.processStartProvider = processStartProvider;

            this.syncthingManager.StateChanged += SyncthingStateChanged;

            NetworkGraph = networkGraph;
            NetworkGraph.ConductWith(this);

            CompletedTransfers = new BindableCollection<FileTransferViewModel>();
            InProgressTransfers = new BindableCollection<FileTransferViewModel>();

            CompletedTransfers.CollectionChanged += (o, e) =>
            {
                NotifyOfPropertyChange(() => HasCompletedTransfers);
                NotifyOfPropertyChange(() => AnyTransfers);
            };
            InProgressTransfers.CollectionChanged += (o, e) =>
            {
                NotifyOfPropertyChange(() => HasInProgressTransfers);
                NotifyOfPropertyChange(() => AnyTransfers);
            };
        }

        protected override void OnActivate()
        {
            foreach (var completedTransfer in syncthingManager.TransferHistory.CompletedTransfers)
            {
                AddCompletedTransfer(new FileTransferViewModel(completedTransfer));
            }

            foreach (var inProgressTranser in syncthingManager.TransferHistory.InProgressTransfers
                         .Where(x => x.Status == FileTransferStatus.InProgress).Reverse())
            {
                InProgressTransfers.Add(new FileTransferViewModel(inProgressTranser));
            }

            // We start caring about samples when they're either finished, or have a progress update
            syncthingManager.TransferHistory.TransferStateChanged += TransferStateChanged;

            UpdateConnectionStats(syncthingManager.TotalConnectionStats);

            syncthingManager.TotalConnectionStatsChanged += TotalConnectionStatsChanged;
        }

        protected override void OnDeactivate()
        {
            syncthingManager.TransferHistory.TransferStateChanged -= TransferStateChanged;

            syncthingManager.TotalConnectionStatsChanged -= TotalConnectionStatsChanged;

            foreach (var fileTransferViewModel in CompletedTransfers)
            {
                fileTransferViewModel.Dispose();
            }

            CompletedTransfers.Clear();
            InProgressTransfers.Clear();
        }

        private void TransferStateChanged(object sender, FileTransferChangedEventArgs e)
        {
            var transferVm = InProgressTransfers.FirstOrDefault(x => x.FileTransfer == e.FileTransfer);
            if (transferVm == null)
            {
                if (e.FileTransfer.Status == FileTransferStatus.Completed)
                    AddCompletedTransfer(new FileTransferViewModel(e.FileTransfer));
                else if (e.FileTransfer.Status == FileTransferStatus.InProgress)
                    InProgressTransfers.Insert(0, new FileTransferViewModel(e.FileTransfer));
                // We don't care about 'starting' transfers
            }
            else
            {
                transferVm.UpdateState();

                if (e.FileTransfer.Status == FileTransferStatus.Completed)
                {
                    InProgressTransfers.Remove(transferVm);
                    AddCompletedTransfer(transferVm);
                }
            }
        }

        private void TotalConnectionStatsChanged(object sender, ConnectionStatsChangedEventArgs e)
        {
            UpdateConnectionStats(e.TotalConnectionStats);
        }

        private void SyncthingStateChanged(object sender, SyncthingStateChangedEventArgs e)
        {
            if (syncthingManager.State == SyncthingState.Running)
                UpdateConnectionStats(0, 0);
            else
                UpdateConnectionStats(null, null);
        }

        private void UpdateConnectionStats(SyncthingConnectionStats connectionStats)
        {
            if (syncthingManager.State == SyncthingState.Running)
                UpdateConnectionStats(connectionStats.InBytesPerSecond, connectionStats.OutBytesPerSecond);
            else
                UpdateConnectionStats(null, null);
        }

        private void UpdateConnectionStats(double? inBytesPerSecond, double? outBytesPerSecond)
        {
            if (inBytesPerSecond == null)
                InConnectionRate = null;
            else
                InConnectionRate = FormatUtils.BytesToHuman(inBytesPerSecond.Value, 1);

            if (outBytesPerSecond == null)
                OutConnectionRate = null;
            else
                OutConnectionRate = FormatUtils.BytesToHuman(outBytesPerSecond.Value, 1);
        }

        public void AddCompletedTransfer(FileTransferViewModel newItem)
        {
            while (CompletedTransfers.Count >= CompletedTransfersToDisplay)
            {
                var last = CompletedTransfers.Last();
                last.Dispose();
                CompletedTransfers.Remove(last);
            }

            CompletedTransfers.Insert(0, newItem);
        }

        public void ItemClicked(FileTransferViewModel fileTransferVm)
        {
            var fileTransfer = fileTransferVm.FileTransfer;

            // Not sure of the best way to deal with deletions yet...
            if (fileTransfer.ActionType == ItemChangedActionType.Update)
            {
                if (fileTransfer.ItemType == ItemChangedItemType.File)
                    processStartProvider.ShowFileInExplorer(Path.Combine(fileTransferVm.Folder.Path,
                        fileTransfer.Path));
                else if (fileTransfer.ItemType == ItemChangedItemType.Dir)
                    processStartProvider.ShowFolderInExplorer(Path.Combine(fileTransferVm.Folder.Path,
                        fileTransfer.Path));
            }
        }

        public void Dispose()
        {
            syncthingManager.StateChanged -= SyncthingStateChanged;

            NetworkGraph.Dispose();
        }
    }
}