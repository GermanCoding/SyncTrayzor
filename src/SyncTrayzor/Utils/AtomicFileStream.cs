using NLog;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SyncTrayzor.Utils
{
    /// <summary>
    /// Writes to a temporary file, which replaces the destination file only once <see cref="Commit"/> has been called
    /// and the stream is disposed. If the stream is disposed without being committed (e.g. because an exception was
    /// thrown while writing), the temporary file is discarded and the destination file is left untouched.
    /// </summary>
    public class AtomicFileStream : FileStream
    {
        private const string DefaultTempFileSuffix = ".tmp";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly string path;
        private readonly string tempPath;
        private readonly string backupPath;
        private bool committed;
        private bool finished;

        /// <param name="path">File to replace</param>
        /// <param name="backupPath">If not null, a copy of the current contents of <paramref name="path"/> is kept
        /// here before it's replaced. This is best-effort: failing to make the copy doesn't fail the write.</param>
        public AtomicFileStream(string path, string backupPath = null)
            : this(path, TempFilePath(path), backupPath)
        {
        }

        public AtomicFileStream(string path, string tempPath, string backupPath)
            : base(tempPath ?? throw new ArgumentNullException(nameof(tempPath)), FileMode.Create, FileAccess.ReadWrite)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.tempPath = tempPath;
            this.backupPath = backupPath;
        }

        private static string TempFilePath(string path)
        {
            return path + DefaultTempFileSuffix;
        }

        /// <summary>
        /// Marks the contents as complete. The data is flushed all the way to disk now (FlushFileBuffers), so that
        /// once the temporary file is renamed over the destination, a crash or power loss can't leave the destination
        /// with the right length but unwritten (zero-filled) contents.
        /// </summary>
        public void Commit()
        {
            Flush(flushToDisk: true);
            committed = true;
        }

        protected override void Dispose(bool disposing)
        {
            // Only touch the filesystem on an explicit Dispose: never from the finalizer, and only once
            if (!disposing || finished)
            {
                base.Dispose(disposing);
                return;
            }
            finished = true;

            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                if (!committed)
                    TryDelete(tempPath);
            }

            if (!committed)
                return;

            if (backupPath != null)
                TryBackUp();

            // A single same-volume rename, so the destination always holds either the old or the new contents
            if (!NativeMethods.MoveFileEx(tempPath, path, MoveFileFlags.ReplaceExisting | MoveFileFlags.WriteThrough))
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        private void TryBackUp()
        {
            try
            {
                if (!File.Exists(path))
                    return;
                File.Copy(path, backupPath, overwrite: true);
                using var backup = new FileStream(backupPath, FileMode.Open, FileAccess.Write);
                backup.Flush(flushToDisk: true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warn(e, "Unable to back up {0} to {1}. Continuing without a backup", path, backupPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warn(e, "Unable to delete temporary file {0}", path);
            }
        }

        [Flags]
        private enum MoveFileFlags
        {
            None = 0,
            ReplaceExisting = 1,
            CopyAllowed = 2,
            DelayUntilReboot = 4,
            WriteThrough = 8,
            CreateHardlink = 16,
            FailIfNotTrackable = 32,
        }

        private static class NativeMethods
        {
            [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool MoveFileEx(
                [In] string lpExistingFileName,
                [In] string lpNewFileName,
                [In] MoveFileFlags dwFlags);
        }
    }
}
