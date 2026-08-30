#nullable enable
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.WindowsAPICodePack.Dialogs;
using NLog;
using Stylet;
using SyncTrayzor.Localization;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Utils;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SyncTrayzor.Pages
{
    public class ViewerViewModel : Screen, IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string webView2DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

        // Configuration stores the zoom level using CEF's logarithmic scale (0 == 100%, each step a factor of
        // 1.2), and we keep doing so to stay compatible with existing config files. WebView2 instead takes a
        // linear multiplier, so we convert on the way in and out.
        private const double zoomLevelBase = 1.2;

        // The environment owns the browser process, and is shared by every WebView2 we create. Creating it is
        // async and can fail (e.g. the runtime isn't installed), so memoise the task rather than the result.
        private static readonly object environmentLock = new();
        private static Task<CoreWebView2Environment>? environmentTask;

        private readonly IWindowManager windowManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IConfigurationProvider configurationProvider;
        private readonly IApplicationPathsProvider pathsProvider;

        private readonly object cultureLock = new(); // This can be read from many threads
        private CultureInfo? culture;
        private double zoomLevel;

        // The address we want to be at. WebView2 can only be navigated once its CoreWebView2 exists, which
        // happens asynchronously, so we record the intent here and apply it whenever we're able to.
        private string location = "about:blank";

        public string Location
        {
            get => location;
            private set
            {
                location = value;
                ApplyLocation();
            }
        }

        private SyncthingState syncthingState { get; set; }
        public bool ShowSyncthingStarting => syncthingState == SyncthingState.Starting;
        public bool ShowSyncthingStopped => syncthingState == SyncthingState.Stopped;

        // WebView2 hosts a native child window, which paints over anything WPF puts in the same grid cell.
        // The overlays above therefore only work if we hide the browser while they're showing.
        public bool ShowBrowser => syncthingState == SyncthingState.Running;

        public WebView2? WebBrowser { get; private set; }

        public ViewerViewModel(
            IWindowManager windowManager,
            ISyncthingManager syncthingManager,
            IConfigurationProvider configurationProvider,
            IProcessStartProvider processStartProvider,
            IApplicationPathsProvider pathsProvider)
        {
            this.windowManager = windowManager;
            this.syncthingManager = syncthingManager;
            this.processStartProvider = processStartProvider;
            this.configurationProvider = configurationProvider;
            this.pathsProvider = pathsProvider;

            var configuration = this.configurationProvider.Load();
            zoomLevel = configuration.SyncthingWebBrowserZoomLevel;

            this.syncthingManager.StateChanged += SyncthingStateChanged;

            SetCulture(configuration);
            configurationProvider.ConfigurationChanged += ConfigurationChanged;
        }

        private void SyncthingStateChanged(object? sender, SyncthingStateChangedEventArgs e)
        {
            syncthingState = e.NewState;
            RefreshBrowser();
        }

        private void ConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            SetCulture(e.NewConfiguration);
        }

        private void SetCulture(Configuration configuration)
        {
            lock (cultureLock)
            {
                culture = configuration.UseComputerCulture
                    ? CultureInfo.CurrentUICulture
                    : CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
            }
        }

        protected override void OnInitialActivate()
        {
            var webBrowser = new WebView2();
            webBrowser.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webBrowser.NavigationStarting += NavigationStarting;
            webBrowser.NavigationCompleted += NavigationCompleted;
            webBrowser.WebMessageReceived += WebMessageReceived;

            WebBrowser = webBrowser;

            RefreshBrowser();

            // Deliberately not awaited: initialisation completes on its own schedule (it can't finish until
            // the control has been given a window handle), and everything that depends on it is event-driven.
            _ = InitializeBrowserAsync(webBrowser);
        }

        private Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            lock (environmentLock)
            {
                if (environmentTask == null)
                {
                    string language;
                    lock (cultureLock)
                    {
                        language = culture!.Name;
                    }

                    var options = new CoreWebView2EnvironmentOptions
                    {
                        Language = language,
                        AreBrowserExtensionsEnabled = false,
                        // System proxy settings (which also specify a proxy for localhost) shouldn't affect us
                        AdditionalBrowserArguments = "--no-proxy-server",
                    };

                    var remoteDebuggingPort = AppSettings.Instance.WebViewRemoteDebuggingPort;
                    if (remoteDebuggingPort != 0)
                    {
                        options.AdditionalBrowserArguments +=
                            $" --remote-debugging-port={remoteDebuggingPort.ToString(CultureInfo.InvariantCulture)}";
                    }

                    // Unlike CEF, WebView2 is happy for several processes to share a user data folder, so we
                    // don't need to guard against a second SyncTrayzor instance using the same one.
                    environmentTask = CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: pathsProvider.WebView2DataPath,
                        options: options);
                }

                return environmentTask;
            }
        }

        private async Task InitializeBrowserAsync(WebView2 webBrowser)
        {
            try
            {
                var environment = await GetEnvironmentAsync();
                await webBrowser.EnsureCoreWebView2Async(environment);
            }
            catch (WebView2RuntimeNotFoundException e)
            {
                logger.Error(e, "The WebView2 runtime is not installed");
                OnWebView2RuntimeMissing();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to initialize WebView2");
                windowManager.ShowMessageBox(
                    Localizer.Translate("ViewerView_BrowserInitializationFailed", e.Message),
                    Localizer.Translate("ViewerView_BrowserInitializationFailed_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnWebView2RuntimeMissing()
        {
            var result = windowManager.ShowMessageBox(
                Localizer.Translate("ViewerView_WebView2RuntimeMissing"),
                Localizer.Translate("ViewerView_WebView2RuntimeMissing_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
                processStartProvider.StartDetached(webView2DownloadUrl);
        }

        private void CoreWebView2InitializationCompleted(object? sender,
            CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess || WebBrowser?.CoreWebView2 == null)
            {
                logger.Error(e.InitializationException, "WebView2 initialization did not succeed");
                return;
            }

            var coreWebView = WebBrowser.CoreWebView2;

            coreWebView.Settings.AreDevToolsEnabled = AppSettings.Instance.WebViewRemoteDebuggingPort != 0;
            coreWebView.Settings.IsStatusBarEnabled = false;
            coreWebView.Settings.IsSwipeNavigationEnabled = false;
            coreWebView.Settings.IsPasswordAutosaveEnabled = false;
            coreWebView.Settings.IsGeneralAutofillEnabled = false;

            // We used to set CefSettings.IgnoreCertificateErrors: Syncthing may redirect us to HTTPS with a
            // self-signed certificate, which is expected and fine for a connection to localhost.
            coreWebView.ServerCertificateErrorDetected += ServerCertificateErrorDetected;

            // See https://github.com/canton7/SyncTrayzor/issues/13 - Syncthing needs the API key on every
            // request, including the resources the page pulls in.
            coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += WebResourceRequested;

            coreWebView.NewWindowRequested += NewWindowRequested;
            coreWebView.ContextMenuRequested += ContextMenuRequested;

            WebBrowser.ZoomFactor = ZoomLevelToFactor(zoomLevel);

            ApplyLocation();
        }

        private void ServerCertificateErrorDetected(object? sender,
            CoreWebView2ServerCertificateErrorDetectedEventArgs e)
        {
            e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
        }

        private void WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (syncthingManager.State != SyncthingState.Running)
                return;

            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
                return;

            if (uri.Host != GetSyncthingAddress().Host)
                return;

            e.Request.Headers.SetHeader("X-API-Key", syncthingManager.ApiKey);
        }

        private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // We can get requests just after changing Syncthing's address: after we've navigated to
            // about:blank but before navigating to the new address (which we do when Syncthing hits the
            // 'running' state). Therefore only open external browsers if Syncthing is actually running.
            if (syncthingManager.State != SyncthingState.Running)
                return;

            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
                return;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return;

            if (uri.Host == GetSyncthingAddress().Host)
                return;

            e.Cancel = true;
            processStartProvider.StartDetached(e.Uri);
        }

        private void NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            processStartProvider.StartDetached(e.Uri);
        }

        private void ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            // Strip Edge's menu back to the three entries SyncTrayzor has always offered. Names are the
            // unlocalized identifiers; the labels Edge renders are already translated for us.
            var keep = new[] { "cut", "copy", "paste" };
            for (var i = e.MenuItems.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(e.MenuItems[i].Name))
                    e.MenuItems.RemoveAt(i);
            }
        }

        private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || WebBrowser?.CoreWebView2 == null)
                return;

            if (WebBrowser.Source == null || WebBrowser.Source.ToString() == "about:blank")
                return;

            // Zoom is per-origin in WebView2 and resets when we navigate somewhere new, so reapply it.
            WebBrowser.ZoomFactor = ZoomLevelToFactor(zoomLevel);

            InjectCustomisations(WebBrowser);
        }

        private static void InjectCustomisations(WebView2 webBrowser)
        {
            // The host-side counterpart of these lives in WebMessageReceived.
            var bridge =
                @"window.syncTrayzor = {" +
                @"  openFolder: function(folderId) {" +
                @"    window.chrome.webview.postMessage({ type: 'openFolder', folderId: folderId });" +
                @"  }," +
                @"  browseFolderPath: function() {" +
                @"    window.chrome.webview.postMessage({ type: 'browseFolderPath' });" +
                @"  }" +
                @"};";
            webBrowser.ExecuteScriptAsync(bridge);

            // I tried to do this using Syncthing's events, but it's very painful - the DOM is updated some time
            // after the event is fired. It's a lot easier to just watch for changes on the DOM.
            var addOpenFolderButton =
                @"var syncTrayzorAddOpenFolderButton = function(elem) {" +
                @"    var $buttonContainer = elem.find('.panel-footer .pull-right');" +
                @"    $buttonContainer.find('.panel-footer .synctrayzor-add-folder-button').remove();" +
                @"    $buttonContainer.prepend(" +
                @"      '<button class=""btn btn-sm btn-default synctrayzor-add-folder-button"" onclick=""syncTrayzor.openFolder(angular.element(this).scope().folder.id)"">" +
                @"          <span class=""fa fa-folder-open""></span>" +
                @"          <span style=""margin-left: 3px"">" + Resources.ViewerView_OpenFolder + @"</span>" +
                @"      </button>');" +
                @"};" +
                @"new MutationObserver(function(mutations, observer) {" +
                @"  for (var i = 0; i < mutations.length; i++) {" +
                @"    for (var j = 0; j < mutations[i].addedNodes.length; j++) {" +
                @"      syncTrayzorAddOpenFolderButton($(mutations[i].addedNodes[j]));" +
                @"    }" +
                @"  }" +
                @"}).observe(document.getElementById('folders'), {" +
                @"  childList: true" +
                @"});" +
                @"syncTrayzorAddOpenFolderButton($('#folders'));" +
                @"";
            webBrowser.ExecuteScriptAsync(addOpenFolderButton);

            var addFolderBrowse =
                @"$('#folderPath').wrap($('<div/>').css('display', 'flex'));" +
                @"$('#folderPath').after(" +
                @"  $('<button>').attr('id', 'folderPathBrowseButton')" +
                @"               .addClass('btn btn-sm btn-default')" +
                @"               .html('" + Resources.ViewerView_BrowseToFolder + @"')" +
                @"               .css({'flex-grow': 1, 'margin': '0 0 0 5px'})" +
                @"               .on('click', function() { syncTrayzor.browseFolderPath() })" +
                @");" +
                @"$('#folderPath').removeAttr('list');" +
                @"$('#directory-list').remove();" +
                @"$('#editFolder').on('shown.bs.modal', function() {" +
                @"  if ($('#folderPath').is('[readonly]')) {" +
                @"      $('#folderPathBrowseButton').attr('disabled', 'disabled');" +
                @"  }" +
                @"  else {" +
                @"      $('#folderPathBrowseButton').removeAttr('disabled');" +
                @"  }" +
                @"});";
            webBrowser.ExecuteScriptAsync(addFolderBrowse);
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string type;
            string? folderId = null;

            try
            {
                using var document = JsonDocument.Parse(e.WebMessageAsJson);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("type", out var typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String)
                {
                    return;
                }

                type = typeElement.GetString()!;

                if (root.TryGetProperty("folderId", out var folderIdElement) &&
                    folderIdElement.ValueKind == JsonValueKind.String)
                {
                    folderId = folderIdElement.GetString();
                }
            }
            catch (JsonException ex)
            {
                logger.Warn(ex, "Received an unparseable message from the Syncthing UI");
                return;
            }

            switch (type)
            {
                case "openFolder" when folderId != null:
                    OpenFolder(folderId);
                    break;
                case "browseFolderPath":
                    BrowseFolderPath();
                    break;
                default:
                    logger.Warn("Received an unknown message from the Syncthing UI: {0}", type);
                    break;
            }
        }

        private void ApplyLocation()
        {
            var webBrowser = WebBrowser;
            if (webBrowser?.CoreWebView2 == null)
                return;

            if (!Uri.TryCreate(location, UriKind.Absolute, out var uri))
                return;

            if (webBrowser.Source == uri)
                return;

            webBrowser.Source = uri;
        }

        public async void RefreshBrowserNukeCache()
        {
            var webBrowser = WebBrowser;

            if (Location == GetSyncthingAddress().ToString())
            {
                if (webBrowser?.CoreWebView2 != null)
                {
                    try
                    {
                        await webBrowser.CoreWebView2.Profile.ClearBrowsingDataAsync(
                            CoreWebView2BrowsingDataKinds.DiskCache);
                    }
                    catch (Exception e)
                    {
                        logger.Warn(e, "Failed to clear the WebView2 disk cache");
                    }

                    // The user may have closed the viewer while we were clearing the cache
                    WebBrowser?.Reload();
                }
            }
            else if (syncthingManager.State == SyncthingState.Running)
            {
                Location = GetSyncthingAddress().ToString();
            }
        }

        public void RefreshBrowser()
        {
            Location = "about:blank";
            if (syncthingManager.State == SyncthingState.Running)
                Location = GetSyncthingAddress().ToString();
        }

        public void ZoomIn()
        {
            ZoomTo(zoomLevel + 0.2);
        }

        public void ZoomOut()
        {
            ZoomTo(zoomLevel - 0.2);
        }

        public void ZoomReset()
        {
            ZoomTo(0.0);
        }

        private static double ZoomLevelToFactor(double zoomLevel)
        {
            return Math.Pow(zoomLevelBase, zoomLevel);
        }

        private void ZoomTo(double zoomLevel)
        {
            if (WebBrowser == null || syncthingState != SyncthingState.Running)
                return;

            this.zoomLevel = zoomLevel;
            WebBrowser.ZoomFactor = ZoomLevelToFactor(zoomLevel);
            configurationProvider.AtomicLoadAndSave(c => c.SyncthingWebBrowserZoomLevel = zoomLevel);
        }

        private void OpenFolder(string folderId)
        {
            if (!syncthingManager.Folders.TryFetchById(folderId, out var folder))
                return;

            processStartProvider.ShowFolderInExplorer(folder.Path);
        }

        private void BrowseFolderPath()
        {
            Execute.OnUIThread(() =>
            {
                var dialog = new CommonOpenFileDialog()
                {
                    IsFolderPicker = true,
                };
                var result = dialog.ShowDialog();
                if (result == CommonFileDialogResult.Ok)
                {
                    // JsonSerializer gives us a correctly quoted and escaped JavaScript string literal
                    var path = JsonSerializer.Serialize(dialog.FileName);
                    var script =
                        @"$('#folderPath').val(" + path + @");" +
                        @"$('#folderPath').change();";
                    WebBrowser?.ExecuteScriptAsync(script);
                }
            });
        }

        protected override void OnClose()
        {
            var webBrowser = WebBrowser;
            WebBrowser = null;

            if (webBrowser != null)
            {
                webBrowser.CoreWebView2InitializationCompleted -= CoreWebView2InitializationCompleted;
                webBrowser.NavigationStarting -= NavigationStarting;
                webBrowser.NavigationCompleted -= NavigationCompleted;
                webBrowser.WebMessageReceived -= WebMessageReceived;

                if (webBrowser.CoreWebView2 != null)
                {
                    webBrowser.CoreWebView2.ServerCertificateErrorDetected -= ServerCertificateErrorDetected;
                    webBrowser.CoreWebView2.WebResourceRequested -= WebResourceRequested;
                    webBrowser.CoreWebView2.NewWindowRequested -= NewWindowRequested;
                    webBrowser.CoreWebView2.ContextMenuRequested -= ContextMenuRequested;
                }

                webBrowser.Dispose();
            }
        }

        public async void Start()
        {
            await syncthingManager.StartWithErrorDialogAsync(windowManager);
        }

        private Uri GetSyncthingAddress()
        {
            // SyncthingManager will always request over HTTPS, whether Syncthing enforces this or not.
            // However in an attempt to avoid #201 we'll use HTTP if available, and if not Syncthing will redirect us.
            var uriBuilder = new UriBuilder(syncthingManager.Address.NormalizeZeroHost())
            {
                Scheme = "http"
            };
            return uriBuilder.Uri;
        }

        public void Dispose()
        {
            syncthingManager.StateChanged -= SyncthingStateChanged;
            configurationProvider.ConfigurationChanged -= ConfigurationChanged;
        }
    }
}
