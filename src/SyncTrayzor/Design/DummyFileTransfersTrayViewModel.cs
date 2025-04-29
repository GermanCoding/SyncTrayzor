﻿using Stylet;
using SyncTrayzor.Pages.Tray;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.Folders;
using SyncTrayzor.Syncthing.TransferHistory;

namespace SyncTrayzor.Design
{
    public class DummyFileTransfersTrayViewModel
    {
        public BindableCollection<FileTransferViewModel> CompletedTransfers { get; private set; }
        public BindableCollection<FileTransferViewModel> InProgressTransfers { get; private set; }

        public bool HasCompletedTransfers => CompletedTransfers.Count > 0;
        public bool HasInProgressTransfers => InProgressTransfers.Count > 0;

        public string InConnectionRate { get; private set; }
        public string OutConnectionRate { get; private set; }

        public bool AnyTransfers { get; private set; }

        public DummyFileTransfersTrayViewModel()
        {
            CompletedTransfers = new BindableCollection<FileTransferViewModel>();
            InProgressTransfers = new BindableCollection<FileTransferViewModel>();
            var folder = new Folder("folder", "Folder", "folderPath", false, FolderSyncState.Syncing, null);

            var completedFileTransfer1 = new FileTransfer(folder, "path.pdf", ItemChangedItemType.File, ItemChangedActionType.Update);
            completedFileTransfer1.SetComplete(null, false);

            var completedFileTransfer2 = new FileTransfer(folder, "a really very long path that's far too long to sit on the page.h", ItemChangedItemType.File, ItemChangedActionType.Delete);
            completedFileTransfer2.SetComplete("Something went very wrong", true);

            //this.CompletedTransfers.Add(new FileTransferViewModel(completedFileTransfer1));
            CompletedTransfers.Add(new FileTransferViewModel(completedFileTransfer2));

            var inProgressTransfer1 = new FileTransfer(folder, "path.txt", ItemChangedItemType.File, ItemChangedActionType.Update);
            inProgressTransfer1.SetDownloadProgress(5*1024*1024, 100*1024*1024);

            var inProgressTransfer2 = new FileTransfer(folder, "path", ItemChangedItemType.Dir, ItemChangedActionType.Update);

            InProgressTransfers.Add(new FileTransferViewModel(inProgressTransfer1));
            InProgressTransfers.Add(new FileTransferViewModel(inProgressTransfer2));

            InConnectionRate = "1.2MB";
            OutConnectionRate = "0.0MB";

            AnyTransfers = true;
        }
    }
}
