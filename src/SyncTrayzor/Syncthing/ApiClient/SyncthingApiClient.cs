﻿using Newtonsoft.Json;
using NLog;
using RestEase;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class SyncthingApiClient : ISyncthingApiClient
    {
        private static readonly Logger logger = LogManager.GetLogger("SyncTrayzor.Syncthing.ApiClient.SyncthingApiClient");
        private ISyncthingApi api;

        public SyncthingApiClient(Uri baseAddress, string apiKey)
        {
            var httpClient = new HttpClient(new SyncthingHttpClientHandler())
            {
                BaseAddress = baseAddress.NormalizeZeroHost(),
                Timeout = TimeSpan.FromSeconds(70),
            };
            api = new RestClient(httpClient)
            {
                JsonSerializerSettings = new JsonSerializerSettings()
                {
                    Converters = { new EventConverter() }
                }
            }.For<ISyncthingApi>();
            api.ApiKey = apiKey;
        }

        public Task ShutdownAsync()
        {
            logger.Debug("Requesting API shutdown");
            return api.ShutdownAsync();
        }

        public Task<List<Event>> FetchEventsAsync(int since, int limit, CancellationToken cancellationToken)
        {
            return api.FetchEventsLimitAsync(since, limit, EventConverter.GetEventsFilterString(), cancellationToken);
        }

        public Task<List<Event>> FetchEventsAsync(int since, CancellationToken cancellationToken)
        {
            return api.FetchEventsAsync(since, EventConverter.GetEventsFilterString(), cancellationToken);
        }

        public async Task<Config> FetchConfigAsync()
        {
            var config = await api.FetchConfigAsync();
            logger.Debug("Fetched configuration: {0}", config);
            return config;
        }

        public Task ScanAsync(string folderId, string subPath)
        {
            logger.Debug("Scanning folder: {0} subPath: {1}", folderId, subPath);
            return api.ScanAsync(folderId, subPath);
        }

        public async Task<SystemInfo> FetchSystemInfoAsync(CancellationToken cancellationToken)
        {
            var systemInfo = await api.FetchSystemInfoAsync(cancellationToken);
            logger.Debug("Fetched system info: {0}", systemInfo);
            return systemInfo;
        }

        public async Task<Connections> FetchConnectionsAsync(CancellationToken cancellationToken)
        {
            var connections = await api.FetchConnectionsAsync(cancellationToken);
            return connections;
        }

        public async Task<SyncthingVersion> FetchVersionAsync(CancellationToken cancellationToken)
        {
            var version = await api.FetchVersionAsync(cancellationToken);
            logger.Debug("Fetched version: {0}", version);
            return version;
        }

        public Task RestartAsync()
        {
            logger.Debug("Restarting Syncthing");
            return api.RestartAsync();
        }

        public async Task<FolderStatus> FetchFolderStatusAsync(string folderId, CancellationToken cancellationToken)
        {
            var folderStatus = await api.FetchFolderStatusAsync(folderId, cancellationToken);
            logger.Debug("Fetched folder status for folder {0}: {1}", folderId, folderStatus);
            return folderStatus;
        }

        public async Task<DebugFacilitiesSettings> FetchDebugFacilitiesAsync()
        {
            var facilities = await api.FetchDebugFacilitiesAsync();
            logger.Debug("Got debug facilities: {0}", facilities);
            return facilities; 
        }

        public Task SetDebugFacilitiesAsync(IEnumerable<string> enable, IEnumerable<string> disable)
        {
            var enabled = String.Join(",", enable);
            var disabled = String.Join(",", disable);
            logger.Debug("Setting trace facilities: enabling {0}; disabling {1}", enabled, disabled);

            return api.SetDebugFacilitiesAsync(enabled, disabled);
        }

        public Task PauseDeviceAsync(string deviceId)
        {
            logger.Debug("Pausing device {0}", deviceId);
            return api.PauseDeviceAsync(deviceId);
        }

        public Task ResumeDeviceAsync(string deviceId)
        {
            logger.Debug("Resuming device {0}", deviceId);
            return api.ResumeDeviceAsync(deviceId);
        }
    }
}
