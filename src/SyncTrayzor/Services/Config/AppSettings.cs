﻿using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SyncTrayzor.Services.Config
{
    public class XmlConfigurationSection : ConfigurationSection
    {
        private XmlReader reader;

        protected override void DeserializeSection(XmlReader reader)
        {
            this.reader = reader;
        }

        protected override object GetRuntimeObject()
        {
            return reader;
        }
    }

    public class AppSettings
    {
        private const string sectionName = "settings";
        private static readonly XmlSerializer serializer = new(typeof(AppSettings), new XmlRootAttribute(sectionName));

        public static readonly AppSettings Instance;

        static AppSettings()
        {
            var reader = (XmlReader)ConfigurationManager.GetSection(sectionName);
            Instance = (AppSettings)serializer.Deserialize(reader);
        }

        public string UpdateApiUrl { get; set; } = "http://synctrayzor.antonymale.co.uk/version-check";
        public string HomepageUrl { get; set; } = "http://github.com/GermanCoding/SyncTrayzor";
        public int DirectoryWatcherBackoffMilliseconds { get; set; } = 2000;
        public int DirectoryWatcherFolderExistenceCheckMilliseconds { get; set; } = 3000;
        public string IssuesUrl { get; set; } = "http://github.com/GermanCoding/SyncTrayzor/issues";
        public bool EnableAutostartOnFirstStart { get; set; } = false;
        public int CefRemoteDebuggingPort { get; set; } = 0;
        public SyncTrayzorVariant Variant { get; set; } = SyncTrayzorVariant.Portable;
        public int SyncthingConnectTimeoutSeconds { get; set; } = 600;
        public bool EnforceSingleProcessPerUser { get; set; } = true;

        public PathConfiguration PathConfiguration { get; set; } = new();

        public Configuration DefaultUserConfiguration { get; set; } = new();

        public override string ToString()
        {
            using var writer = new StringWriter();
            serializer.Serialize(writer, this);
            return writer.ToString();
        }
    }
}
