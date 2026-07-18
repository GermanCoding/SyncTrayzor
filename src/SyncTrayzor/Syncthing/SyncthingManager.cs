using NLog;
using RestEase;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.EventWatcher;
using SyncTrayzor.Syncthing.TransferHistory;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SyncTrayzor.Syncthing.Devices;
using SyncTrayzor.Syncthing.Folders;

namespace SyncTrayzor.Syncthing
{
    public interface ISyncthingManager : IDisposable
    {
        SyncthingState State { get; }
        bool IsDataLoaded { get; }
        event EventHandler DataLoaded;
        event EventHandler<SyncthingStateChangedEventArgs> StateChanged;
        event EventHandler<MessageLoggedEventArgs> MessageLogged;
        SyncthingConnectionStats TotalConnectionStats { get; }
        event EventHandler<ConnectionStatsChangedEventArgs> TotalConnectionStatsChanged;
        event EventHandler ProcessExitedWithError;
        event EventHandler<DeviceRejectedEventArgs> DeviceRejected;
        event EventHandler<FolderRejectedEventArgs> FolderRejected;

        string ExecutablePath { get; set; }
        string ApiKey { get; }
        string PreferredHostAndPort { get; set; }
        Uri Address { get; set; }
        List<string> SyncthingCommandLineFlags { get; set; }
        IDictionary<string, string> SyncthingEnvironmentalVariables { get; set; }
        string SyncthingCustomHomeDir { get; set; }
        bool SyncthingDenyUpgrade { get; set; }
        SyncthingPriorityLevel SyncthingPriorityLevel { get; set; }
        bool SyncthingHideDeviceIds { get; set; }
        TimeSpan SyncthingConnectTimeout { get; set; }
        DateTime StartedTime { get; }
        DateTime LastConnectivityEventTime { get; }
        SyncthingVersionInformation Version { get; }
        ISyncthingFolderManager Folders { get; }
        ISyncthingDeviceManager Devices { get; }
        ISyncthingTransferHistory TransferHistory { get; }
        ISyncthingCapabilities Capabilities { get; }
        bool DoSyncthingUpgrade { get; set; }

        Task StartAsync();
        Task StopAsync();
        Task StopAndWaitAsync();
        Task RestartAsync();
        void Kill();
        void KillAllSyncthingProcesses();

        Task ScanAsync(string folderId, string subPath);
    }

    public class SyncthingManager : ISyncthingManager
    {
        private const string apiKeyChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";
        private const int apiKeyLength = 40;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly SynchronizedEventDispatcher eventDispatcher;
        private readonly ISyncthingProcessRunner processRunner;
        private readonly ISyncthingApiClientFactory apiClientFactory;

        // This lock covers the eventWatcher, connectionsWatcher, apiClients, and the CTS
        private readonly object apiClientsLock = new();
        private readonly ISyncthingEventWatcher eventWatcher;
        private readonly ISyncthingConnectionsWatcher connectionsWatcher;
        private readonly SynchronizedTransientWrapper<ISyncthingApiClient> apiClient;
        private readonly IFreePortFinder freePortFinder;
        private CancellationTokenSource apiAbortCts;

        private DateTime _startedTime;
        private readonly object startedTimeLock = new();
        public DateTime StartedTime
        {
            get { lock (startedTimeLock) { return _startedTime; } }
            set { lock (startedTimeLock) { _startedTime = value; } }
        }

        private DateTime _lastConnectivityEventTime;
        private readonly object lastConnectivityEventTimeLock = new();
        public DateTime LastConnectivityEventTime
        {
            get { lock (lastConnectivityEventTimeLock) { return _lastConnectivityEventTime; } }
            private set { lock (lastConnectivityEventTimeLock) { _lastConnectivityEventTime = value; } }
        }

        private readonly object stateLock = new();
        private SyncthingState _state;
        public SyncthingState State
        {
            get { lock (stateLock) { return _state; } }
            set { lock (stateLock) { _state = value; } }
        }

        private SystemInfo systemInfo;

        public bool IsDataLoaded { get; private set; }
        public bool DoSyncthingUpgrade { get; set; }
        public event EventHandler DataLoaded;
        public event EventHandler<SyncthingStateChangedEventArgs> StateChanged;
        public event EventHandler<MessageLoggedEventArgs> MessageLogged;
        public event EventHandler<DeviceRejectedEventArgs> DeviceRejected;
        public event EventHandler<FolderRejectedEventArgs> FolderRejected;

        private readonly object totalConnectionStatsLock = new();
        private SyncthingConnectionStats _totalConnectionStats = new(0, 0, 0, 0);
        public SyncthingConnectionStats TotalConnectionStats
        {
            get { lock (totalConnectionStatsLock) { return _totalConnectionStats; } }
            set { lock (totalConnectionStatsLock) { _totalConnectionStats = value; } }
        }
        public event EventHandler<ConnectionStatsChangedEventArgs> TotalConnectionStatsChanged;

        public event EventHandler ProcessExitedWithError;

        public string ExecutablePath { get; set; }
        public string ApiKey { get; }
        public string PreferredHostAndPort { get; set; }
        public Uri Address { get; set; }
        public string SyncthingCustomHomeDir { get; set; }
        public List<string> SyncthingCommandLineFlags { get; set; } = new();
        public IDictionary<string, string> SyncthingEnvironmentalVariables { get; set; } = new Dictionary<string, string>();
        public bool SyncthingDenyUpgrade { get; set; }
        public SyncthingPriorityLevel SyncthingPriorityLevel { get; set; }
        public bool SyncthingHideDeviceIds { get; set; }
        public TimeSpan SyncthingConnectTimeout { get; set; }

        public SyncthingVersionInformation Version { get; private set; }

        private readonly SyncthingFolderManager _folders;
        public ISyncthingFolderManager Folders => _folders;

        private readonly SyncthingDeviceManager _devices;
        public ISyncthingDeviceManager Devices => _devices;

        private readonly ISyncthingTransferHistory _transferHistory;
        public ISyncthingTransferHistory TransferHistory => _transferHistory;

        private readonly SyncthingCapabilities _capabilities = new();
        public ISyncthingCapabilities Capabilities => _capabilities;

        public SyncthingManager(
            ISyncthingProcessRunner processRunner,
            ISyncthingApiClientFactory apiClientFactory,
            ISyncthingEventWatcherFactory eventWatcherFactory,
            ISyncthingConnectionsWatcherFactory connectionsWatcherFactory,
            IFreePortFinder freePortFinder)
        {
            StartedTime = DateTime.MinValue;
            LastConnectivityEventTime = DateTime.MinValue;

            ApiKey = GenerateApiKey();

            eventDispatcher = new SynchronizedEventDispatcher(this);
            this.processRunner = processRunner;
            this.apiClientFactory = apiClientFactory;
            this.freePortFinder = freePortFinder;

            apiClient = new SynchronizedTransientWrapper<ISyncthingApiClient>(apiClientsLock);

            eventWatcher = eventWatcherFactory.CreateEventWatcher(apiClient);
            eventWatcher.DeviceConnected += (o, e) => LastConnectivityEventTime = DateTime.UtcNow;
            eventWatcher.DeviceDisconnected += (o, e) => LastConnectivityEventTime = DateTime.UtcNow;
            eventWatcher.ConfigSaved += (o, e) => ReloadConfigDataAsync();
            eventWatcher.EventsSkipped += (o, e) => ReloadConfigDataAsync();
            eventWatcher.DeviceRejected += (o, e) => OnDeviceRejected(e.DeviceId, e.Address);
            eventWatcher.FolderRejected += (o, e) => OnFolderRejected(e.DeviceId, e.FolderId);

            connectionsWatcher = connectionsWatcherFactory.CreateConnectionsWatcher(apiClient);
            connectionsWatcher.TotalConnectionStatsChanged += (o, e) => OnTotalConnectionStatsChanged(e.TotalConnectionStats);

            _folders = new SyncthingFolderManager(apiClient, eventWatcher);
            _devices = new SyncthingDeviceManager(apiClient, eventWatcher, Capabilities);
            _transferHistory = new SyncthingTransferHistory(eventWatcher, _folders);

            this.processRunner.ProcessStopped += (o, e) => ProcessStopped(e.ExitStatus);
            this.processRunner.MessageLogged += (o, e) => OnMessageLogged(e.LogMessage);
            this.processRunner.ProcessRestarted += (o, e) => ProcessRestarted();
            this.processRunner.Starting += (o, e) => ProcessStarting();
        }

        public async Task StartAsync()
        {
            processRunner.Start();
            await StartClientAsync();
        }

        public async Task StopAsync()
        {
            var apiClient = this.apiClient.Value;
            if (State != SyncthingState.Running || apiClient == null)
                return;

            // Syncthing can stop so quickly that it doesn't finish sending the response to us
            try
            {
                await apiClient.ShutdownAsync();
            }
            catch (HttpRequestException)
            { }

            SetState(SyncthingState.Stopping);
        }

        public async Task StopAndWaitAsync()
        {
            var apiClient = this.apiClient.Value;
            if (State != SyncthingState.Running || apiClient == null)
                return;

            var tcs = new TaskCompletionSource<object>();
            EventHandler<SyncthingStateChangedEventArgs> stateChangedHandler = (o, e) =>
            {
                if (e.NewState == SyncthingState.Stopped)
                    tcs.TrySetResult(null);
                else if (e.NewState != SyncthingState.Stopping)
                    tcs.TrySetException(new Exception($"Failed to stop Syncthing: Went to state {e.NewState} instead"));
            };

            StateChanged += stateChangedHandler;
            try
            {
                // Syncthing can stop so quickly that it doesn't finish sending the response to us
                try
                {
                    await apiClient.ShutdownAsync();
                }
                catch (HttpRequestException)
                { }

                SetState(SyncthingState.Stopping);

                await tcs.Task;
            }
            finally
            {
                StateChanged -= stateChangedHandler;
            }
        }

        public async Task RestartAsync()
        {
            if (State != SyncthingState.Running)
                return;

            // Syncthing can stop so quickly that it doesn't finish sending the response to us
            try
            {
                await apiClient.Value.RestartAsync();
            }
            catch (HttpRequestException)
            {
            }
        }

        public void Kill()
        {
            processRunner.Kill();
            SetState(SyncthingState.Stopped);
        }

        public void KillAllSyncthingProcesses()
        {
            processRunner.KillAllSyncthingProcesses();
        }  

        public async Task ScanAsync(string folderId, string subPath)
        {
            try
            {
                await apiClient.Value.ScanAsync(folderId, subPath);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                logger.Error(e, "Error scanning folder {folderId} with path {subPath}", folderId, subPath);
            }
        }

        private void SetState(SyncthingState state)
        {
            SyncthingState oldState;
            bool abortApi = false;
            lock (stateLock)
            {
                logger.Debug("Request to set state: {0} -> {1}", _state, state);
                if (state == _state)
                    return;

                oldState = _state;
                // We really need a proper state machine here....
                // There's a race if Syncthing can't start because the database is locked by another process on the same port
                // In this case, we see the process as having failed, but the event watcher chimes in a split-second later with the 'Started' event.
                // This runs the risk of transitioning us from Stopped -> Starting -> Stopped -> Running, which is bad news for everyone
                // So, get around this by enforcing strict state transitions.
                if (_state == SyncthingState.Stopped && state == SyncthingState.Running)
                    return;

                // Not entirely sure where this condition comes from...
                if (_state == SyncthingState.Stopped && state == SyncthingState.Stopping)
                    return;

                if (_state == SyncthingState.Running ||
                    (_state == SyncthingState.Starting && state == SyncthingState.Stopped))
                    abortApi = true;

                logger.Debug("Setting state: {0} -> {1}", _state, state);
                _state = state;
            }

            eventDispatcher.Raise(StateChanged, new SyncthingStateChangedEventArgs(oldState, state));

            if (abortApi)
            {
                logger.Debug("Aborting API clients");
                // StopApiClients acquires the correct locks, and aborts the CTS
                StopApiClients();
            }
        }

        private string GenerateApiKey()
        {
            var random = new Random();
            var apiKey = new char[apiKeyLength];
            for (int i = 0; i < apiKeyLength; i++)
            {
                apiKey[i] = apiKeyChars[random.Next(apiKeyChars.Length)];
            }
            return new string(apiKey);
        }

        private async Task CreateApiClientAsync(CancellationToken cancellationToken)
        {
            logger.Debug("Starting API clients");
            var apiClient = await apiClientFactory.CreateCorrectApiClientAsync(Address, ApiKey, SyncthingConnectTimeout, cancellationToken);
            logger.Debug("Have the API client! It's {0}", apiClient.GetType().Name);

            this.apiClient.Value = apiClient;

            SetState(SyncthingState.Running);
        }

        private async Task StartClientAsync()
        {
            try
            {
                apiAbortCts = new CancellationTokenSource();
                await CreateApiClientAsync(apiAbortCts.Token);
                await LoadStartupDataAsync(apiAbortCts.Token);
                StartWatchers(apiAbortCts.Token);
            }
            catch (OperationCanceledException) // If Syncthing dies on its own, etc
            {
                logger.Debug("StartClientAsync aborted");
            }
            catch (ApiException e)
            {
                var msg = $"RestEase Error. StatusCode: {e.StatusCode}. Content: {e.Content}. Reason: {e.ReasonPhrase}";
                logger.Error(e, msg);
                throw new SyncthingDidNotStartCorrectlyException(msg, e);
            }
            catch (HttpRequestException e)
            {
                var msg = $"HttpRequestException while starting Syncthing: {e.Message}";
                logger.Error(e, msg);
                throw new SyncthingDidNotStartCorrectlyException(msg, e);
            }
            catch (Exception e)
            {
                var msg = $"Unexpected exception while starting Syncthing: {e.Message}";
                logger.Error(e, msg);
                throw new SyncthingDidNotStartCorrectlyException(msg, e);
            }
        }

        private void StartWatchers(CancellationToken cancellationToken)
        {
            // This is all synchronous, so it's safe to execute inside the lock
            lock (apiClientsLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (apiClient == null)
                    throw new InvalidOperationException("ApiClient must not be null");

                connectionsWatcher.Start();
                eventWatcher.Start();
            }
        }

        private void StopApiClients()
        {
            lock (apiClientsLock)
            {
                if (apiAbortCts != null)
                    apiAbortCts.Cancel();

                apiClient.UnsynchronizedValue = null;

                connectionsWatcher.Stop();
                eventWatcher.Stop();
            }
        }

        private async void ProcessStarting()
        {
            var preferredAddressWithScheme = new Uri("https://" + PreferredHostAndPort);
            var port = freePortFinder.FindFreePort(preferredAddressWithScheme.Port);
            var uriBuilder = new UriBuilder(preferredAddressWithScheme)
            {
                Port = port
            };
            Address = uriBuilder.Uri;

            processRunner.ApiKey = ApiKey;
            // Don't pass a scheme here - we want Syncthing to choose http / https as appropriate
            processRunner.HostAddress = $"{Address.Host}:{Address.Port}";
            processRunner.ExecutablePath = ExecutablePath;
            processRunner.CustomHomeDir = SyncthingCustomHomeDir;
            processRunner.CommandLineFlags = SyncthingCommandLineFlags;
            processRunner.EnvironmentalVariables = SyncthingEnvironmentalVariables;
            processRunner.DenyUpgrade = SyncthingDenyUpgrade;
            processRunner.SyncthingPriorityLevel = SyncthingPriorityLevel;
            processRunner.HideDeviceIds = SyncthingHideDeviceIds;

            if (DoSyncthingUpgrade)
            {
                processRunner.UpgradeSyncthing();
                DoSyncthingUpgrade = false;
            }

            var isRestart = (State == SyncthingState.Restarting);
            SetState(SyncthingState.Starting);

            // Catch restart cases, and re-start the API
            // This isn't ideal, as we don't get to nicely propagate any exceptions to the UI
            if (isRestart)
            {
                try
                {
                    await StartClientAsync();
                }
                catch (SyncthingDidNotStartCorrectlyException)
                {
                    // We've already logged this
                }
            }
        }

        private void ProcessStopped(SyncthingExitStatus exitStatus)
        {
            SetState(SyncthingState.Stopped);
            if (exitStatus == SyncthingExitStatus.Error)
                OnProcessExitedWithError();
        }

        private void ProcessRestarted()
        {
            SetState(SyncthingState.Restarting);
        }

        private async Task LoadStartupDataAsync(CancellationToken cancellationToken)
        {
            logger.Debug("Startup Complete! Loading startup data");

            // There's a race where Syncthing died, and so we kill the API clients and set it to null,
            // but we still end up here, because threading.
            var apiClient = this.apiClient.Value;
            cancellationToken.ThrowIfCancellationRequested();

            var syncthingVersionTask = apiClient.FetchVersionAsync(cancellationToken);
            var systemInfoTask = apiClient.FetchSystemInfoAsync(cancellationToken);

            await Task.WhenAll(syncthingVersionTask, systemInfoTask);

            systemInfo = systemInfoTask.Result;
            var syncthingVersion = syncthingVersionTask.Result;

            Version = new SyncthingVersionInformation(syncthingVersion.Version, syncthingVersion.LongVersion);
            _capabilities.SyncthingVersion = Version.ParsedVersion;
            
            cancellationToken.ThrowIfCancellationRequested();

            await LoadConfigDataAsync(systemInfo.Tilde, false, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            
            StartedTime = DateTime.UtcNow;
            IsDataLoaded = true;
            OnDataLoaded();
        }

        private async Task LoadConfigDataAsync(string tilde, bool isReload, CancellationToken cancellationToken)
        {
            // We can end up here just as Syncthing is restarting
            var apiClient = this.apiClient.Value;
            cancellationToken.ThrowIfCancellationRequested();

            var config = await apiClient.FetchConfigAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (isReload)
            {
                await Task.WhenAll(_folders.ReloadFoldersAsync(config, tilde, cancellationToken), _devices.ReloadDevicesAsync(config, cancellationToken));
            }
            else
            {
                await Task.WhenAll(_folders.LoadFoldersAsync(config, tilde, cancellationToken), _devices.LoadDevicesAsync(config, cancellationToken));
            }
        }

        private async void ReloadConfigDataAsync()
        {
            // Shit. We don't know what state any of our folders are in. We'll have to poll them all....
            // Note that we're executing on the ThreadPool here: we don't have a Task route back to the main thread.
            // Any exceptions are ours to manage....

            // HttpRequestException, ApiException, and  OperationCanceledException are more or less expected: Syncthing could shut down
            // at any point

            try
            { 
                await LoadConfigDataAsync(systemInfo.Tilde, true, CancellationToken.None);
            }
            catch (HttpRequestException)
            { }
            catch (OperationCanceledException)
            { }
            catch (ApiException)
            { }
        }

        private void OnMessageLogged(string logMessage)
        {
            eventDispatcher.Raise(MessageLogged, new MessageLoggedEventArgs(logMessage));
        }

        private void OnTotalConnectionStatsChanged(SyncthingConnectionStats stats)
        {
            TotalConnectionStats = stats;
            eventDispatcher.Raise(TotalConnectionStatsChanged, new ConnectionStatsChangedEventArgs(stats));
        }

        private void OnDataLoaded()
        {
            eventDispatcher.Raise(DataLoaded);
        }

        private void OnProcessExitedWithError()
        {
            eventDispatcher.Raise(ProcessExitedWithError);
        }

        private void OnDeviceRejected(string deviceId, string address)
        {
            eventDispatcher.Raise(DeviceRejected, new DeviceRejectedEventArgs(deviceId, address));
        }

        private void OnFolderRejected(string deviceId, string folderId)
        {
            if (!Devices.TryFetchById(deviceId, out var device))
                return;

            if (!Folders.TryFetchById(folderId, out var folder))
                return;

            eventDispatcher.Raise(FolderRejected, new FolderRejectedEventArgs(device, folder));
        }

        public void Dispose()
        {
            processRunner.Dispose();
            StopApiClients();
            eventWatcher.Dispose();
            connectionsWatcher.Dispose();
        }
    }
}
