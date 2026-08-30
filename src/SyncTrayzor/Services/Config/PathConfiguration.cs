using System.Xml.Serialization;

namespace SyncTrayzor.Services.Config
{
    [XmlRoot("PathConfiguration")]
    public class PathConfiguration
    {
        public string LogFilePath { get; set; }
        public string ConfigurationFilePath { get; set; }
        public string ConfigurationFileBackupPath { get; set; }
        public string WebView2DataPath { get; set; }
        public string SyncthingPath { get; set; }
        public string SyncthingHomePath { get; set; }

        public PathConfiguration()
        {
            LogFilePath = @"logs";
            ConfigurationFilePath = @"data\config.xml";
            ConfigurationFileBackupPath = @"data\config-backups";
            WebView2DataPath = @"data\webview2";
            SyncthingPath = @"data\syncthing.exe";
            SyncthingHomePath = @"data\syncthing";
        }
    }
}
