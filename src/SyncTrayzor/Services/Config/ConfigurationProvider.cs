using NLog;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SyncTrayzor.Services.Config
{
    public class ConfigurationChangedEventArgs : EventArgs
    {
        private readonly Configuration baseConfiguration;
        public Configuration NewConfiguration => new(baseConfiguration);

        public ConfigurationChangedEventArgs(Configuration newConfiguration)
        {
            baseConfiguration = newConfiguration;
        }
    }

    public interface IConfigurationProvider
    {
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        bool HadToCreateConfiguration { get; }
        bool WasUpgraded { get; }
        bool SyncthingInstalled { get; }

        void Initialize(Configuration defaultConfiguration);
        Configuration Load();
        void Save(Configuration config);
        void AtomicLoadAndSave(Action<Configuration> setter);
    }

    public class ConfigurationProvider : IConfigurationProvider
    {
        // Together these come to half a second, which is probably sensible
        private const int fileSaveRetryCount = 10;
        private const int fileSaveFailureDelayMs = 50;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly XmlSerializer serializer = new(typeof(Configuration));

        private readonly Func<XDocument, XDocument>[] migrations;
        private readonly SynchronizedEventDispatcher eventDispatcher;
        private readonly IApplicationPathsProvider paths;
        private readonly IFilesystemProvider filesystem;
        private readonly IPathTransformer pathTransformer;

        private readonly object currentConfigLock = new();
        private Configuration currentConfig;

        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        public bool HadToCreateConfiguration { get; private set; }
        public bool WasUpgraded { get; private set; }
        public bool SyncthingInstalled { get; private set; }

        public ConfigurationProvider(IApplicationPathsProvider paths, IFilesystemProvider filesystemProvider, IPathTransformer pathTransformer)
        {
            this.paths = paths;
            filesystem = filesystemProvider;
            this.pathTransformer = pathTransformer;
            eventDispatcher = new SynchronizedEventDispatcher(this);

            migrations = new Func<XDocument, XDocument>[]
            {
                MigrateV1ToV2,
                MigrateV2ToV3,
                MigrateV3ToV4,
                MigrateV4ToV5,
                MigrateV5ToV6,
                MigrateV6ToV7,
                MigrateV7ToV8,
                MigrateV8ToV9,
                MigrateV9ToV10,
            };
        }

        public void Initialize(Configuration defaultConfiguration)
        {
            if (defaultConfiguration == null)
                throw new ArgumentNullException("defaultConfiguration");

            // If this fails, then we're going to show an error. However, other parts of the application may still try and load the configuration.
            // Therefore ensure that *something* is in place!
            try
            {
                if (!filesystem.DirectoryExists(Path.GetDirectoryName(paths.ConfigurationFilePath)))
                    filesystem.CreateDirectory(Path.GetDirectoryName(paths.ConfigurationFilePath));

                currentConfig = LoadFromDisk(defaultConfiguration, out bool hadToCreateConfiguration);
                HadToCreateConfiguration = hadToCreateConfiguration;
            }
            catch
            {
                currentConfig = defaultConfiguration;
                throw;
            }

            bool updateConfigInstallCount = false;
            int latestInstallCount = 0;
            // Might be portable, in which case this file won't exist
            if (filesystem.FileExists(paths.InstallCountFilePath))
            {
                latestInstallCount = Int32.Parse(filesystem.ReadAllText(paths.InstallCountFilePath).Trim());
                if (latestInstallCount != currentConfig.LastSeenInstallCount)
                {
                    logger.Debug("InstallCount changed from {0} to {1}", currentConfig.LastSeenInstallCount, latestInstallCount);
                    WasUpgraded = true;
                    updateConfigInstallCount = true;
                }
            }

            // This is duplicated between here and ConfigurationApplicator, and it's ugly.
            var usingDefaultSyncthingPath = string.IsNullOrWhiteSpace(currentConfig.SyncthingCustomPath);
            var expandedSyncthingPath = usingDefaultSyncthingPath ?
                paths.DefaultSyncthingPath :
                pathTransformer.MakeAbsolute(currentConfig.SyncthingCustomPath);

            // They might be the same if we're portable, in which case, nothing to do
            if (!filesystem.FileExists(expandedSyncthingPath))
            {
                if (!filesystem.FileExists(paths.SyncthingBackupPath))
                    throw new CouldNotFindSyncthingException(paths.SyncthingBackupPath);

                logger.Warn("Syncthing doesn't exist at {0}, so copying from {1}", expandedSyncthingPath, paths.SyncthingBackupPath);

                var expandedSyncthingPathDir = Path.GetDirectoryName(expandedSyncthingPath);
                if (!filesystem.DirectoryExists(expandedSyncthingPathDir))
                    filesystem.CreateDirectory(expandedSyncthingPathDir);

                filesystem.Copy(paths.SyncthingBackupPath, expandedSyncthingPath);
                SyncthingInstalled = true;
            }
            else if (usingDefaultSyncthingPath &&
                     !String.Equals(Path.GetFullPath(expandedSyncthingPath), Path.GetFullPath(paths.SyncthingBackupPath), StringComparison.OrdinalIgnoreCase) &&
                     filesystem.FileExists(paths.SyncthingBackupPath))
            {
                var installedVersion = filesystem.GetFileVersion(expandedSyncthingPath);
                var bundledVersion = filesystem.GetFileVersion(paths.SyncthingBackupPath);
                if (installedVersion != null && bundledVersion != null && bundledVersion > installedVersion)
                {
                    logger.Info("Updating the default Syncthing binary at {Path} from {InstalledVersion} to bundled version {BundledVersion}",
                        expandedSyncthingPath, installedVersion, bundledVersion);
                    filesystem.Copy(paths.SyncthingBackupPath, expandedSyncthingPath, overwrite: true);
                    SyncthingInstalled = true;
                }
            }

            if (updateConfigInstallCount)
            {
                currentConfig.LastSeenInstallCount = latestInstallCount;
                SaveToFile(currentConfig);
            }
        }

        private Configuration LoadFromDisk(Configuration defaultConfiguration, out bool hadToCreate)
        {
            hadToCreate = false;

            // Merge any updates from app.config / Configuration into the configuration file on disk
            // (creating if necessary)
            logger.Info("Loaded default configuration: {0}", defaultConfiguration);
            XDocument defaultConfig;
            using (var ms = new System.IO.MemoryStream())
            {
                serializer.Serialize(ms, defaultConfiguration);
                ms.Position = 0;
                defaultConfig = XDocument.Load(ms);
            }

            Configuration configuration;
            try
            {
                XDocument loadedConfig;
                if (filesystem.FileExists(paths.ConfigurationFilePath))
                {
                    logger.Debug("Found existing configuration at {0}", paths.ConfigurationFilePath);
                    using (var stream = filesystem.OpenRead(paths.ConfigurationFilePath))
                    {
                        loadedConfig = XDocument.Load(stream);
                    }
                    loadedConfig = MigrateConfiguration(loadedConfig);

                    var merged = loadedConfig.Root.Elements().Union(defaultConfig.Root.Elements(), new XmlNodeComparer());
                    loadedConfig.Root.ReplaceNodes(merged);
                }
                else
                {
                    logger.Info($"Configuration file {paths.ConfigurationFilePath} doesn't exist, so creating");
                    hadToCreate = true;
                    loadedConfig = defaultConfig;
                }

                configuration = (Configuration)serializer.Deserialize(loadedConfig.CreateReader());
            }
            catch (Exception e)
            {
                throw new BadConfigurationException(paths.ConfigurationFilePath, e);
            }

            SaveToFile(configuration);

            return configuration;
        }

        private XDocument MigrateConfiguration(XDocument configuration)
        {
            var version = (int?)configuration.Root.Attribute("Version");
            if (version == null)
            {
                configuration = LegacyMigrationConfiguration(configuration);
                version = 1;
            }

            // Element 0 is the migration from 0 -> 1, etc
            for (int i = version.Value; i < Configuration.CurrentVersion; i++)
            {
                logger.Info("Migrating config version {0} to {1}", i, i + 1);

                if (paths.ConfigurationFileBackupPath != null)
                {
                    if (!filesystem.FileExists(paths.ConfigurationFileBackupPath))
                        filesystem.CreateDirectory(paths.ConfigurationFileBackupPath);
                    var backupPath = Path.Combine(paths.ConfigurationFileBackupPath, $"config-v{i}.xml");
                    logger.Debug("Backing up configuration to {0}", backupPath);
                    configuration.Save(backupPath);
                }
                
                configuration = migrations[i - 1](configuration);
                configuration.Root.SetAttributeValue("Version", i + 1);
            }

            return configuration;
        }

        private XDocument MigrateV1ToV2(XDocument configuration)
        {
            var traceElement = configuration.Root.Element("SyncthingTraceFacilities");
            // No need to remove - it'll be ignored when we deserialize into Configuration, and not written back to file
            if (traceElement != null)
            {
                var envVarsNode = new XElement("SyncthingEnvironmentalVariables",
                    new XElement("Item",
                        new XElement("Key", "STTRACE"),
                        new XElement("Value", traceElement.Value)
                    )
                );
                var existingEnvVars = configuration.Root.Element("SyncthingEnvironmentalVariables");
                if (existingEnvVars != null)
                    existingEnvVars.ReplaceWith(envVarsNode);
                else
                    configuration.Root.Add(envVarsNode);
            }

            return configuration;
        }

        private XDocument MigrateV2ToV3(XDocument configuration)
        {
            bool? visible = (bool?)configuration.Root.Element("ShowSyncthingConsole");
            configuration.Root.Add(new XElement("SyncthingConsoleHeight", visible == true ? Configuration.DefaultSyncthingConsoleHeight : 0.0));
            return configuration;
        }

        private XDocument MigrateV3ToV4(XDocument configuration)
        {
            // No need to remove - it'll be ignored when we deserialize into Configuration, and not written back to file
            bool? showNotifications = (bool?)configuration.Root.Element("ShowSynchronizedBalloon");
            var folders = configuration.Root.Element("Folders").Elements("Folder");
            foreach (var folder in folders)
            {
                folder.Add(new XElement("ShowSynchronizedBalloon", showNotifications.GetValueOrDefault(true)));
            }
            return configuration;
        }

        private XDocument MigrateV4ToV5(XDocument configuration)
        {
            bool? runLowPriority = (bool?)configuration.Root.Element("SyncthingRunLowPriority");
            // No need to remove - it'll be ignored when we deserialize into Configuration, and not written back to file
            configuration.Root.Add(new XElement("SyncthingPriorityLevel", runLowPriority == true ? "BelowNormal" : "Normal"));
            return configuration;
        }

        private XDocument MigrateV5ToV6(XDocument configuration)
        {
            // If the SyncthingPath was previously %EXEPATH%\syncthing.exe, and we're portable,
            // change it to %EXEPATH%\data\syncthing.exe
            if (AppSettings.Instance.Variant == SyncTrayzorVariant.Portable)
            {
                var pathElement = configuration.Root.Element("SyncthingPath");
                if (pathElement.Value == @"%EXEPATH%\syncthing.exe")
                    pathElement.Value = @"%EXEPATH%\data\syncthing.exe";
            }
            return configuration;
        }

        private XDocument MigrateV6ToV7(XDocument configuration)
        {
            // Take STTRACE values, and put them in SyncthingDebugFacilities
            var envVarsElement = configuration.Root.Element("SyncthingEnvironmentalVariables");
            var debugFacilitiesElement = new XElement("SyncthingDebugFacilities");
            configuration.Root.Add(debugFacilitiesElement);

            var traceFacilitiesElement = envVarsElement.Elements("Item").Where(x => (string)x.Element("Key") == "STTRACE").FirstOrDefault();
            if (traceFacilitiesElement != null)
            {
                traceFacilitiesElement.Remove();
                foreach (var traceFacility in ((string)traceFacilitiesElement.Element("Value")).Split(','))
                {
                    debugFacilitiesElement.Add(
                        new XElement("DebugFacility", traceFacility)
                    );
                }
            }

            return configuration;
        }

        private XDocument MigrateV7ToV8(XDocument configuration)
        {
            // Get rid of %EXEPATH%
            var syncthingPath = configuration.Root.Element("SyncthingPath");
            syncthingPath.Value = syncthingPath.Value.TrimStart("%EXEPATH%\\");

            var syncthingCustomHomePath = configuration.Root.Element("SyncthingCustomHomePath");
            syncthingCustomHomePath.Value = syncthingCustomHomePath.Value.TrimStart("%EXEPATH%\\");

            return configuration;
        }

        private XDocument MigrateV8ToV9(XDocument configuration)
        {
            // The installed version defaulted to "don't use custom home", while the portable
            // version defaulted to using it.
            // Therefore if we were installed, clear SyncthingCustomHomePath if they opted not to use it.
            // If we were portable:
            // - If they were using the custom home path (the default), clear it if it equals SyncthingHomePath
            // - If they weren't using a custom home, that means they went back to using syncthing's default dir,
            //   so set that.

            // We want to hard-code these paths, as app.config may change in future, but this migration must
            // always do the same thing.

            bool useCustomHome = (bool)configuration.Root.Element("SyncthingUseCustomHome");
            var customHomePath = configuration.Root.Element("SyncthingCustomHomePath").Value;
            string result;

            if (AppSettings.Instance.Variant == SyncTrayzorVariant.Installed)
            {
                result = useCustomHome ? customHomePath : String.Empty;
            }
            else
            {
                if (useCustomHome)
                    result = (customHomePath == @"data\syncthing") ? String.Empty : customHomePath;
                else
                    result = @"%LOCALAPPDATA%\Syncthing";
            }

            configuration.Root.Element("SyncthingCustomHomePath").Value = result;

            // SyncthingUseCustomHome will be removed when it's deserialized into Configuration

            return configuration;
        }

        private XDocument MigrateV9ToV10(XDocument configuration)
        {
            // If SyncthingPath differs from the default, write it to SyncthingCustomPath.
            // Otherwise leave it to be dropped.

            var syncthingPath = configuration.Root.Element("SyncthingPath").Value;
            if (!String.Equals(syncthingPath, paths.UnexpandedDefaultSyncthingPath, StringComparison.OrdinalIgnoreCase))
                configuration.Root.Add(new XElement("SyncthingCustomPath", syncthingPath));

            return configuration;
        }

        private XDocument LegacyMigrationConfiguration(XDocument configuration)
        {
            var address = configuration.Root.Element("SyncthingAddress").Value;

            // We used to store http/https in the config, but we no longer do. A migration is necessary
            if (address.StartsWith("http://"))
                configuration.Root.Element("SyncthingAddress").Value = address.Substring("http://".Length);
            else if (address.StartsWith("https://"))
                configuration.Root.Element("SyncthingAddress").Value = address.Substring("https://".Length);

            return configuration;
        }

        public Configuration Load()
        {
            lock (currentConfigLock)
            {
                return new Configuration(currentConfig);
            }
        }

        public void Save(Configuration config)
        {
            logger.Debug("Saving configuration: {0}", config);
            lock (currentConfigLock)
            {
                currentConfig = config;
                SaveToFile(config);
            }
            OnConfigurationChanged(config);
        }

        public void AtomicLoadAndSave(Action<Configuration> setter)
        {
            // We can just let them modify the current config here - since it's all inside the lock
            Configuration newConfig;
            lock (currentConfigLock)
            {
                setter(currentConfig);
                logger.Debug("Saving configuration atomically: {0}", currentConfig);
                SaveToFile(currentConfig);
                newConfig = currentConfig;
            }
            OnConfigurationChanged(newConfig);
        }

        private void SaveToFile(Configuration config)
        {
            Exception lastException = null;
            for (int i = 0; i < fileSaveRetryCount; i++)
            {
                try
                {
                    using var stream = filesystem.CreateAtomic(paths.ConfigurationFilePath);
                    serializer.Serialize(stream, config);
                    break;
                }
                catch (System.IO.IOException e)
                {
                    lastException = e;
                    logger.Warn("Unable to save config file: maybe someone else has locked it. Trying again shortly", e);
                    Thread.Sleep(fileSaveFailureDelayMs);
                }
            }

            if (lastException != null)
                throw new CouldNotSaveConfigurationExeption(paths.ConfigurationFilePath, lastException);
        }

        private void OnConfigurationChanged(Configuration newConfiguration)
        {
            eventDispatcher.Raise(ConfigurationChanged, new ConfigurationChangedEventArgs(newConfiguration));
        }

        private class XmlNodeComparer : IEqualityComparer<XElement>
        {
            public bool Equals(XElement x, XElement y) => x.Name == y.Name;

            public int GetHashCode(XElement obj) => obj.Name.GetHashCode();
        }
    }

    public class CouldNotFindSyncthingException : Exception
    {
        public string SyncthingPath { get; }

        public CouldNotFindSyncthingException(string syncthingPath)
            : base($"Could not find syncthing.exe at {syncthingPath}")
        {
            SyncthingPath = syncthingPath;
        }
    }

    public class BadConfigurationException : Exception
    {
        public string ConfigurationFilePath { get; }

        public BadConfigurationException(string configurationFilePath, Exception innerException)
            : base($"Error deserializing configuration file at {configurationFilePath}", innerException)
        {
            ConfigurationFilePath = configurationFilePath;
        }
    }

    public class CouldNotSaveConfigurationExeption : Exception
    {
        public string ConfigurationFilePath { get; }

        public CouldNotSaveConfigurationExeption(string configurationFilePath, Exception innerException)
            : base($"Could not save configuration file to {configurationFilePath}", innerException)
        {
            ConfigurationFilePath = configurationFilePath;
        }
    }
}
