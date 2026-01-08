#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using SyncTrayzor.Syncthing.Devices;
using SyncTrayzor.Syncthing;
using System.Reactive.Linq;

namespace SyncTrayzor.Services.Conflicts
{
    public class ConflictFile
    {
        public string FilePath { get; }
        public DateTime? LastModified { get; }
        public long? SizeBytes { get; }
        public bool Deleted { get; }

        public ConflictFile(string filePath, DateTime? lastModified, long? sizeBytes, bool deleted)
        {
            FilePath = filePath;
            LastModified = lastModified;
            SizeBytes = sizeBytes;
            Deleted = deleted;
        }

        public override string ToString()
        {
            return FilePath;
        }
    }

    public class ConflictOption
    {
        public string FilePath { get; }
        public DateTime LastModified { get; }

        public DateTime Created { get; }
        public long SizeBytes { get; }
        public Device? Device { get; }

        public ConflictOption(string filePath, DateTime lastModified, DateTime created, long sizeBytes, Device? device)
        {
            FilePath = filePath;
            LastModified = lastModified;
            Created = created;
            SizeBytes = sizeBytes;
            Device = device;
        }

        public override string ToString()
        {
            return FilePath;
        }
    }

    public class ConflictSet
    {
        public ConflictFile File { get; }
        public List<ConflictOption> Conflicts { get; }

        public ConflictSet(ConflictFile file, List<ConflictOption> conflicts)
        {
            File = file;
            Conflicts = conflicts;
        }
    }

    public struct ParsedConflictFileInfo
    {
        public string FilePath { get; }
        public string OriginalPath { get; }
        public DateTime Created { get; }
        public string? ShortDeviceId { get; }
        public bool BaseFileDeleted { get; }

        public ParsedConflictFileInfo(string filePath, string originalPath, DateTime created, string? shortDeviceId,
            bool baseFileDeleted)
        {
            FilePath = filePath;
            OriginalPath = originalPath;
            Created = created;
            ShortDeviceId = shortDeviceId;
            BaseFileDeleted = baseFileDeleted;
        }
    }

    public interface IConflictFileManager
    {
        string ConflictPattern { get; }

        IObservable<ConflictSet> FindConflicts(string basePath);
        void ResolveConflict(ConflictSet conflictSet, string chosenFilePath, bool deleteToRecycleBin);
        bool TryParseConflictFile(string filePath, out ParsedConflictFileInfo parsedConflictFileInfo);
        bool IsPathIgnored(string path);
        bool IsFileIgnored(string path);
    }

    public class ConflictFileManager : IConflictFileManager
    {
        private const string conflictPattern = "*.sync-conflict-*";

        private const string stVersionsFolder = ".stversions";
        private const string syncthingSpecialFileMarker = "~syncthing~";

        private static readonly Regex conflictRegex =
            new(
                @"^(?<prefix>.*)\.sync-conflict-(?<year>\d{4})(?<month>\d{2})(?<day>\d{2})-(?<hours>\d{2})(?<mins>\d{2})(?<secs>\d{2})-(?<device>[a-zA-Z0-9]+)?(?<suffix>.*(?=\.))?(?<extension>\..*)?$");

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const int maxSearchDepth = 255; // Loosely based on the max path length (a bit over)

        private readonly IFilesystemProvider filesystemProvider;
        private readonly ISyncthingManager syncthingManager;

        public string ConflictPattern => conflictPattern;

        public ConflictFileManager(IFilesystemProvider filesystemProvider, ISyncthingManager syncthingManager)
        {
            this.filesystemProvider = filesystemProvider;
            this.syncthingManager = syncthingManager;
        }

        public IObservable<ConflictSet> FindConflicts(string basePath)
        {
            return Observable.Create<ConflictSet>(async (observer, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() =>
                {
                    try
                    {
                        FindConflictsImpl(basePath, observer, cancellationToken);
                        observer.OnCompleted();
                    }
                    catch (Exception e)
                    {
                        observer.OnError(e);
                    }
                });
            });
        }

        public bool IsPathIgnored(string path)
        {
            return path.EndsWith("\\" + stVersionsFolder) || path.Contains("\\" + stVersionsFolder + "\\");
        }

        public bool IsFileIgnored(string path)
        {
            return Path.GetFileName(path).Contains(syncthingSpecialFileMarker);
        }

        private void FindConflictsImpl(string basePath, IObserver<ConflictSet> observer,
            CancellationToken cancellationToken)
        {
            // We may find may conflict files for each conflict, and we need to group them.
            // We can't relay on the order returns by EnumerateFiles either, so it's hard to tell when we've spotted
            // all conflict files. Therefore we need to do this directory by directory, and flush out the cache
            // or conflicts after each directory.

            logger.Debug("Looking for conflicts in {0}", basePath);

            var conflictLookup = new Dictionary<string, List<ParsedConflictFileInfo>>();
            var stack = new Stack<SearchDirectory>();
            stack.Push(new SearchDirectory(basePath, 0));
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                conflictLookup.Clear();
                var searchDirectory = stack.Pop();
                var directory = searchDirectory.Directory;

                TryFilesystemEnumeration(() =>
                {
                    foreach (var filePath in filesystemProvider.EnumerateFiles(directory, conflictPattern,
                                 System.IO.SearchOption.TopDirectoryOnly))
                    {
                        if (IsFileIgnored(filePath))
                            continue;

                        // We may not be able to parse it properly (conflictPattern is pretty basic), or it might not exist, or...
                        if (!TryParseConflictFile(filePath, out var conflictFileInfo))
                            continue;

                        if (!conflictLookup.TryGetValue(conflictFileInfo.OriginalPath, out var existingConflicts))
                        {
                            existingConflicts = new List<ParsedConflictFileInfo>();
                            conflictLookup.Add(conflictFileInfo.OriginalPath, existingConflicts);
                        }

                        existingConflicts.Add(conflictFileInfo);

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }, directory, "directories");

                foreach (var kvp in conflictLookup)
                {
                    // The file can have disappeared between us finding it, and this
                    try
                    {
                        var baseFileDeleted = kvp.Value.Any(conflictinfo => conflictinfo.BaseFileDeleted);
                        ConflictFile file;
                        if (baseFileDeleted)
                        {
                            file = new ConflictFile(kvp.Key, null, null, true);
                        }
                        else
                        {
                            file = new ConflictFile(kvp.Key, filesystemProvider.GetLastWriteTime(kvp.Key),
                                filesystemProvider.GetFileSize(kvp.Key), false);
                        }

                        var devices = syncthingManager.Devices.FetchDevices();
                        var conflicts = kvp.Value.Select(x =>
                        {
                            var device = x.ShortDeviceId == null
                                ? null
                                : devices.FirstOrDefault(d => d.ShortDeviceId == x.ShortDeviceId);
                            return new ConflictOption(x.FilePath, filesystemProvider.GetLastWriteTime(x.FilePath),
                                x.Created, filesystemProvider.GetFileSize(x.FilePath), device);
                        }).ToList();
                        observer.OnNext(new ConflictSet(file, conflicts));
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception e)
                    {
                        logger.Error(e,
                            $"Error while trying to access {kvp.Key}, maybe it was deleted since we scanned it?");
                    }
                }

                if (searchDirectory.Depth < maxSearchDepth)
                {
                    TryFilesystemEnumeration(() =>
                    {
                        foreach (var subDirectory in filesystemProvider.EnumerateDirectories(directory, "*",
                                     System.IO.SearchOption.TopDirectoryOnly))
                        {
                            if (IsPathIgnored(subDirectory))
                                continue;

                            stack.Push(new SearchDirectory(subDirectory, searchDirectory.Depth + 1));

                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }, directory, "files");
                }
                else
                {
                    logger.Warn(
                        $"Max search depth of {maxSearchDepth} exceeded with path {directory}. Not proceeding further.");
                }
            }
        }

        private void TryFilesystemEnumeration(Action action, string path, string itemType)
        {
            try
            {
                action();
            }
            catch (UnauthorizedAccessException)
            {
                // Expected with reparse points, etc
                logger.Warn($"UnauthorizedAccessException when trying to enumerate {itemType} in folder {path}");
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to enumerate {itemType} in folder {path}: {e.GetType().Name} {e.Message}");
            }
        }

        public bool TryParseConflictFile(string filePath, out ParsedConflictFileInfo parsedConflictFileInfo)
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            var parsed = conflictRegex.Match(fileName);
            if (!parsed.Success || directory == null)
            {
                parsedConflictFileInfo = default;
                return false;
            }

            var prefix = parsed.Groups["prefix"].Value;
            var year = Int32.Parse(parsed.Groups["year"].Value);
            var month = Int32.Parse(parsed.Groups["month"].Value);
            var day = Int32.Parse(parsed.Groups["day"].Value);
            var hours = Int32.Parse(parsed.Groups["hours"].Value);
            var mins = Int32.Parse(parsed.Groups["mins"].Value);
            var secs = Int32.Parse(parsed.Groups["secs"].Value);
            var device = parsed.Groups["device"].Value;
            if (string.IsNullOrWhiteSpace(device))
                device = null;
            var suffix = parsed.Groups["suffix"].Value;
            var extension = parsed.Groups["extension"].Value;

            DateTime dateCreated;
            try
            {
                dateCreated = new DateTime(year, month, day, hours, mins, secs, DateTimeKind.Local);
            }
            catch (ArgumentException e)
            {
                // 31st Feb, etc
                logger.Error("Failed to parse DateTime for file path {FilePath}: {e}", filePath, e);
                parsedConflictFileInfo = default(ParsedConflictFileInfo);
                return false;
            }

            // 'suffix' might be a versioner thing (~date-time), or it might be something added by another tool...
            // Try searching for it, and if that fails go without

            try
            {
                var withSuffix = Path.Combine(directory, prefix + suffix + extension);
                if (filesystemProvider.FileExists(withSuffix))
                {
                    parsedConflictFileInfo = new ParsedConflictFileInfo(filePath, withSuffix, dateCreated, device, false);
                    return true;
                }

                var withoutSuffix = Path.Combine(directory, prefix + extension);
                if (filesystemProvider.FileExists(withoutSuffix))
                {
                    parsedConflictFileInfo = new ParsedConflictFileInfo(filePath, withoutSuffix, dateCreated, device, false);
                }
                else
                {
                    // This looks like a conflict file whose base file was deleted
                    parsedConflictFileInfo = new ParsedConflictFileInfo(filePath, withoutSuffix, dateCreated, device, true);
                }

                return true;
            }
            catch (Exception e)
            {
                // We're in the path to return false at this point
                logger.Error(e, $"Failed to look for base file for conflict file {filePath}: {e.Message}");
            }

            parsedConflictFileInfo = default;
            return false;
        }

        public void ResolveConflict(ConflictSet conflictSet, string chosenFilePath, bool deleteToRecycleBin)
        {
            if (chosenFilePath != conflictSet.File.FilePath && conflictSet.Conflicts.All(x => x.FilePath != chosenFilePath))
                throw new ArgumentException("chosenPath does not exist inside conflictSet");

            if (chosenFilePath == conflictSet.File.FilePath)
            {
                foreach (var file in conflictSet.Conflicts)
                {
                    logger.Debug("Deleting {0}", file);
                    DeleteFile(file.FilePath, deleteToRecycleBin);
                }
            }
            else
            {
                logger.Debug("Deleting {0}", conflictSet.File.FilePath);
                DeleteFile(conflictSet.File.FilePath, deleteToRecycleBin);

                foreach (var file in conflictSet.Conflicts)
                {
                    if (file.FilePath == chosenFilePath)
                        continue;

                    logger.Debug("Deleting {0}", file.FilePath);
                    DeleteFile(file.FilePath, deleteToRecycleBin);
                }

                logger.Debug("Renaming {0} to {1}", chosenFilePath, conflictSet.File.FilePath);
                filesystemProvider.MoveFile(chosenFilePath, conflictSet.File.FilePath);
            }
        }

        private void DeleteFile(string path, bool deleteToRecycleBin)
        {
            if (deleteToRecycleBin)
                filesystemProvider.DeleteFileToRecycleBin(path);
            else
                filesystemProvider.DeleteFile(path);
        }

        private struct SearchDirectory
        {
            public readonly string Directory;
            public readonly int Depth;

            public SearchDirectory(string directory, int depth)
            {
                Directory = directory;
                Depth = depth;
            }
        }
    }
}