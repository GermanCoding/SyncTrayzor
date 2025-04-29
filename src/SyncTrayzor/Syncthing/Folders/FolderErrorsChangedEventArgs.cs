﻿using System;
using System.Collections.Generic;

namespace SyncTrayzor.Syncthing.Folders
{
    public class FolderErrorsChangedEventArgs : EventArgs
    {
        public string FolderId { get; }
        public IReadOnlyList<FolderError> Errors { get; }

        public FolderErrorsChangedEventArgs(string folderId, List<FolderError> folderErrors)
        {
            FolderId = folderId;
            Errors = folderErrors.AsReadOnly();
        }
    }
}
