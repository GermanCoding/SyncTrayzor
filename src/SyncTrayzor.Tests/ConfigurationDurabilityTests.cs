using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Utils;
using Xunit;

namespace SyncTrayzor.Tests
{
    public sealed class ConfigurationDurabilityTests : IDisposable
    {
        private readonly string dir;
        private readonly string configPath;
        private string LastGoodPath => configPath + ".lastgood";
        private string DamagedPath => configPath + ".damaged";

        public ConfigurationDurabilityTests()
        {
            dir = Path.Combine(Path.GetTempPath(), "SyncTrayzorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            configPath = Path.Combine(dir, "config.xml");
        }

        public void Dispose()
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }

        // ── AtomicFileStream ────────────────────────────────────────────────

        [Fact]
        public void AtomicFileStream_CommitReplacesFileAndKeepsBackup()
        {
            File.WriteAllText(configPath, "old");
            var backup = configPath + ".bak";

            WriteAtomic(configPath, "new", backup);

            Assert.Equal("new", File.ReadAllText(configPath));
            Assert.Equal("old", File.ReadAllText(backup));
            Assert.False(File.Exists(configPath + ".tmp"));
        }

        [Fact]
        public void AtomicFileStream_WithoutCommitLeavesFileUntouched()
        {
            File.WriteAllText(configPath, "old");

            Action writeThenFail = () =>
            {
                using var stream = new AtomicFileStream(configPath);
                stream.WriteByte(0);
                throw new InvalidOperationException("serialization failed half way");
            };
            Assert.Throws<InvalidOperationException>(writeThenFail);

            Assert.Equal("old", File.ReadAllText(configPath));
            Assert.False(File.Exists(configPath + ".tmp"));
        }

        [Fact]
        public void AtomicFileStream_CommitWorksWhenDestinationDoesNotExist()
        {
            WriteAtomic(configPath, "x", configPath + ".bak");

            Assert.Equal("x", File.ReadAllText(configPath));
            Assert.False(File.Exists(configPath + ".bak"));
        }

        [Fact]
        public void AtomicFileStream_LockedBackupDoesNotPreventWrite()
        {
            File.WriteAllText(configPath, "old");
            var backup = configPath + ".bak";
            File.WriteAllText(backup, "older");

            using (new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                WriteAtomic(configPath, "new", backup);
            }

            Assert.Equal("new", File.ReadAllText(configPath));
            Assert.Equal("older", File.ReadAllText(backup));
        }

        [Fact]
        public void AtomicFileStream_ThrowsWhenDestinationCannotBeReplaced()
        {
            File.WriteAllText(configPath, "old");

            // Open without FILE_SHARE_DELETE, as e.g. an antivirus scanner might
            using (new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Action write = () => WriteAtomic(configPath, "new", backupPath: null);
                var e = Record.Exception(write);
                Assert.True(e is IOException || e is UnauthorizedAccessException, $"Unexpected exception {e}");
            }

            Assert.Equal("old", File.ReadAllText(configPath));
        }

        private static void WriteAtomic(string path, string contents, string backupPath)
        {
            using var stream = new AtomicFileStream(path, backupPath);
            var bytes = Encoding.UTF8.GetBytes(contents);
            stream.Write(bytes, 0, bytes.Length);
            stream.Commit();
        }

        // ── ConfigurationProvider: saving ───────────────────────────────────

        [Fact]
        public void Save_KeepsPreviousVersionAsLastGood()
        {
            var provider = CreateInitializedProvider();
            provider.AtomicLoadAndSave(c => c.SyncthingAddress = "127.0.0.1:1111");
            provider.AtomicLoadAndSave(c => c.SyncthingAddress = "127.0.0.1:2222");

            Assert.Contains("127.0.0.1:2222", File.ReadAllText(configPath));
            Assert.Contains("127.0.0.1:1111", File.ReadAllText(LastGoodPath));
        }

        [Fact]
        public async Task Save_RetriesUntilBrieflyLockedFileIsReleased()
        {
            var provider = CreateInitializedProvider();

            var locked = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var release = Task.Run(() => { Thread.Sleep(200); locked.Dispose(); });

            provider.AtomicLoadAndSave(c => c.SyncthingAddress = "127.0.0.1:3333");
            await release;

            Assert.Contains("127.0.0.1:3333", File.ReadAllText(configPath));
        }

        [Fact]
        public void Save_ReportsCouldNotSaveWhenFileStaysLocked()
        {
            var provider = CreateInitializedProvider();

            using (new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<CouldNotSaveConfigurationExeption>(() =>
                    provider.AtomicLoadAndSave(c => c.SyncthingAddress = "127.0.0.1:4444"));
            }

            // The existing file is untouched
            Assert.DoesNotContain("127.0.0.1:4444", File.ReadAllText(configPath));
            Assert.StartsWith("<?xml", ReadConfigText());
        }

        // ── ConfigurationProvider: loading and recovery ─────────────────────

        [Fact]
        public void Load_RecoversFromZeroFilledConfig()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");
            File.WriteAllText(DamagedPath, "stale file from an earlier recovery");

            // What an unclean shutdown leaves behind: right length, all zeros
            File.WriteAllBytes(configPath, new byte[new FileInfo(configPath).Length]);

            var provider = CreateInitializedProvider();

            // Recovered from the backup, which is the save before last
            Assert.Equal("127.0.0.1:1111", provider.Load().SyncthingAddress);
            Assert.False(provider.HadToCreateConfiguration);
            Assert.All(File.ReadAllBytes(DamagedPath), b => Assert.Equal(0, b));
            Assert.StartsWith("<?xml", ReadConfigText());
        }

        [Fact]
        public void Load_RecoversFromPartiallyZeroedConfig()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");

            var bytes = File.ReadAllBytes(configPath);
            Array.Clear(bytes, bytes.Length / 2, bytes.Length - bytes.Length / 2);
            File.WriteAllBytes(configPath, bytes);

            Assert.Equal("127.0.0.1:1111", CreateInitializedProvider().Load().SyncthingAddress);
        }

        [Fact]
        public void Load_RecoversFromEmptyConfig()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");
            File.WriteAllBytes(configPath, Array.Empty<byte>());

            Assert.Equal("127.0.0.1:1111", CreateInitializedProvider().Load().SyncthingAddress);
        }

        [Fact]
        public void Load_DoesNotSilentlyDiscardHandEditTypos()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");
            var text = ReadConfigText().Replace("</SyncthingAddress>", "</SyncthingAdress>");
            File.WriteAllText(configPath, text);

            Assert.Throws<BadConfigurationException>(() => CreateInitializedProvider());
            Assert.Equal(text, File.ReadAllText(configPath));
            Assert.False(File.Exists(DamagedPath));
        }

        [Fact]
        public void Load_ReportsErrorWhenDamagedAndNoBackup()
        {
            File.WriteAllBytes(configPath, new byte[100]);

            Assert.Throws<BadConfigurationException>(() => CreateInitializedProvider());
            Assert.True(File.Exists(configPath));
        }

        [Fact]
        public void Load_ReportsErrorWhenDamagedAndBackupAlsoDamaged()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");
            File.WriteAllBytes(configPath, new byte[100]);
            File.WriteAllBytes(LastGoodPath, new byte[100]);

            Assert.Throws<BadConfigurationException>(() => CreateInitializedProvider());
            Assert.True(File.Exists(configPath));
        }

        [Fact]
        public void Load_LeavesConfigInPlaceWhenBackupParsesButCannotBeLoaded()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");
            File.WriteAllBytes(configPath, new byte[100]);
            // Well-formed XML, but from an unsupported (newer) version
            File.WriteAllText(LastGoodPath, File.ReadAllText(LastGoodPath).Replace("Version=\"10\"", "Version=\"99\""));

            var e = Assert.Throws<BadConfigurationException>(() => CreateInitializedProvider());
            Assert.IsType<System.Xml.XmlException>(e.InnerException);
            Assert.True(File.Exists(configPath));
            Assert.False(File.Exists(DamagedPath));
        }

        [Fact]
        public void Load_MissingConfigStartsFromDefaultsEvenIfBackupExists()
        {
            SaveTwice("127.0.0.1:1111", "127.0.0.1:2222");
            File.Delete(configPath);

            var provider = CreateInitializedProvider();

            Assert.True(provider.HadToCreateConfiguration);
            Assert.Equal(new Configuration().SyncthingAddress, provider.Load().SyncthingAddress);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private void SaveTwice(string firstAddress, string secondAddress)
        {
            var provider = CreateInitializedProvider();
            provider.AtomicLoadAndSave(c => c.SyncthingAddress = firstAddress);
            provider.AtomicLoadAndSave(c => c.SyncthingAddress = secondAddress);
        }

        private string ReadConfigText() => File.ReadAllText(configPath).TrimStart('﻿');

        private ConfigurationProvider CreateInitializedProvider()
        {
            var syncthingPath = Path.Combine(dir, "syncthing.exe");
            if (!File.Exists(syncthingPath))
                File.WriteAllText(syncthingPath, "");
            var provider = new ConfigurationProvider(new FakePaths(dir), new FilesystemProvider(), new IdentityPathTransformer());
            provider.Initialize(new Configuration());
            return provider;
        }

        private sealed class FakePaths : IApplicationPathsProvider
        {
            private readonly string dir;
            public FakePaths(string dir) => this.dir = dir;

            public string LogFilePath => dir;
            public string SyncthingBackupPath => Path.Combine(dir, "syncthing.exe");
            public string ConfigurationFilePath => Path.Combine(dir, "config.xml");
            public string ConfigurationFileBackupPath => Path.Combine(dir, "config-backups");
            public string UpdatesDownloadPath => dir;
            public string InstallCountFilePath => Path.Combine(dir, "InstallCount.txt");
            public string CefCachePath => dir;
            public string DefaultSyncthingPath => Path.Combine(dir, "syncthing.exe");
            public string DefaultSyncthingHomePath => dir;
            public string UnexpandedDefaultSyncthingPath => "syncthing.exe";
            public void Initialize(PathConfiguration pathConfiguration) { }
        }

        private sealed class IdentityPathTransformer : IPathTransformer
        {
            public string MakeAbsolute(string input) => input;
        }
    }
}
