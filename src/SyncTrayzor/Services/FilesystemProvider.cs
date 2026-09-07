using SyncTrayzor.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace SyncTrayzor.Services
{
    public interface IFilesystemProvider
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        FileStream Open(string path, FileMode mode);
        FileStream Open(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare);
        FileStream CreateAtomic(string path);
        FileStream OpenRead(string path);
        void Copy(string from, string to, bool overwrite = false);
        Version GetFileVersion(string path);
        void MoveFile(string from, string to);
        void CreateDirectory(string path);
        void DeleteFile(string path);
        void DeleteFileToRecycleBin(string path);
        void DeleteDirectory(string path, bool recursive);
        void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
        DateTime GetLastAccessTimeUtc(string path);
        DateTime GetLastWriteTime(string path);
        string[] GetFiles(string path);
        string ReadAllText(string path);
        string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
        string[] GetDirectories(string path);
        string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);
        IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
        long GetFileSize(string path);
    }

    public class FilesystemProvider : IFilesystemProvider
    {
        private const int maxPath = 260;

        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public FileStream Open(string path, FileMode mode) => File.Open(path, mode);

        public FileStream Open(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) => new(path, fileMode, fileAccess, fileShare);

        public FileStream CreateAtomic(string path) => new AtomicFileStream(path);

        public FileStream OpenRead(string path) => File.OpenRead(path);

        public void Copy(string from, string to, bool overwrite = false) => File.Copy(from, to, overwrite);

        public Version GetFileVersion(string path)
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion?.Trim();
            return Version.TryParse(fileVersion, out var version) ? version : null;
        }

        public void MoveFile(string from, string to) => File.Move(from, to);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void DeleteFile(string path) => File.Delete(path);

        public void DeleteFileToRecycleBin(string path)
        {
            // This won't work with paths > MAX_PATH
            if (path.Length >= maxPath)
                File.Delete(path);
            else
                RecycleBinDeleter.Delete(path);
        }

        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

        public void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => File.SetLastAccessTimeUtc(path, lastAccessTimeUtc);

        public DateTime GetLastAccessTimeUtc(string path) => File.GetLastAccessTimeUtc(path);

        public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);

        public string[] GetFiles(string path) => Directory.GetFiles(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);

        public string[] GetDirectories(string path) => Directory.GetDirectories(path);

        public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => Directory.GetDirectories(path, searchPattern, searchOption);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            => Directory.EnumerateFiles(path, searchPattern, searchOption);

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
            => Directory.EnumerateDirectories(path, searchPattern, searchOption);

        public long GetFileSize(string path) => new FileInfo(path).Length;
    }
}
