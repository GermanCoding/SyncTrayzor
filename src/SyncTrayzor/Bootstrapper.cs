using FluentValidation;
using NLog;
using Stylet;
using StyletIoC;
using SyncTrayzor.NotifyIcon;
using SyncTrayzor.Pages;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Services.Conflicts;
using SyncTrayzor.Services.UpdateManagement;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.EventWatcher;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using SyncTrayzor.Services.Metering;
using System.Reflection;
using SyncTrayzor.Localization;
using SyncTrayzor.Services.Ipc;
using System.Net;
using System.Runtime.Versioning;
using System.Windows.Media;
using System.Windows.Interop;

[assembly: SupportedOSPlatform("windows10.0.17763.0")]

namespace SyncTrayzor
{
    public class Bootstrapper : Bootstrapper<ShellViewModel>
    {
        private CommandLineOptionsParser options;
        private bool exiting;

        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            builder.Bind<IApplicationState>().ToInstance(new ApplicationState(Application));
            builder.Bind<IApplicationWindowState>().To<ApplicationWindowState>().InSingletonScope();
            builder.Bind<IFocusWindowProvider>().ToInstance(new FocusWindowProvider(Application));
            builder.Bind<IUserActivityMonitor>().To<UserActivityMonitor>().InSingletonScope();
            builder.Bind<IConfigurationProvider>().To<ConfigurationProvider>().InSingletonScope();
            builder.Bind<IApplicationPathsProvider>().To<ApplicationPathsProvider>().InSingletonScope();
            builder.Bind<IAssemblyProvider>().To<AssemblyProvider>().InSingletonScope();
            builder.Bind<IAutostartProvider>().To<AutostartProvider>().InSingletonScope();
            builder.Bind<ConfigurationApplicator>().ToSelf().InSingletonScope();
            builder.Bind<ISyncthingApiClientFactory>().To<SyncthingApiClientFactory>();
            builder.Bind<ISyncthingEventWatcherFactory>().To<SyncthingEventWatcherFactory>();
            builder.Bind<ISyncthingProcessRunner>().To<SyncthingProcessRunner>().InSingletonScope();
            builder.Bind<ISyncthingManager>().To<SyncthingManager>().InSingletonScope();
            builder.Bind<ISyncthingConnectionsWatcherFactory>().To<SyncthingConnectionsWatcherFactory>();
            builder.Bind<IFreePortFinder>().To<FreePortFinder>().InSingletonScope();
            builder.Bind<INotifyIconManager>().To<NotifyIconManager>().InSingletonScope();
            builder.Bind<IWatchedFolderMonitor>().To<WatchedFolderMonitor>().InSingletonScope();
            builder.Bind<IUpdateManager>().To<UpdateManager>().InSingletonScope();
            builder.Bind<IUpdateDownloader>().To<UpdateDownloader>().InSingletonScope();
            builder.Bind<IUpdateCheckerFactory>().To<UpdateCheckerFactory>();
            builder.Bind<IUpdatePromptProvider>().To<UpdatePromptProvider>();
            builder.Bind<IUpdateNotificationClientFactory>().To<UpdateNotificationClientFactory>();
            builder.Bind<IInstallerCertificateVerifier>().To<InstallerCertificateVerifier>().InSingletonScope();
            builder.Bind<IProcessStartProvider>().To<ProcessStartProvider>().InSingletonScope();
            builder.Bind<IFilesystemProvider>().To<FilesystemProvider>().InSingletonScope();
            builder.Bind<IConflictFileManager>().To<ConflictFileManager>(); // Could be singleton... Not often used
            builder.Bind<IConflictFileWatcher>().To<ConflictFileWatcher>().InSingletonScope();
            builder.Bind<IAlertsManager>().To<AlertsManager>().InSingletonScope();
            builder.Bind<IIpcCommsClientFactory>().To<IpcCommsClientFactory>().InSingletonScope();
            builder.Bind<IIpcCommsServer>().To<IpcCommsServer>().InSingletonScope();
            builder.Bind<IFileWatcherFactory>().To<FileWatcherFactory>();
            builder.Bind<IDirectoryWatcherFactory>().To<DirectoryWatcherFactory>();
            builder.Bind<INetworkCostManager>().To<NetworkCostManager>();
            builder.Bind<IMeteredNetworkManager>().To<MeteredNetworkManager>().InSingletonScope();
            builder.Bind<IPathTransformer>().To<PathTransformer>().InSingletonScope();
            builder.Bind<IConnectedEventDebouncer>().To<ConnectedEventDebouncer>();
            builder.Bind<IDonationManager>().To<DonationManager>().InSingletonScope();

            if (AppSettings.Instance.Variant == SyncTrayzorVariant.Installed)
                builder.Bind<IUpdateVariantHandler>().To<InstalledUpdateVariantHandler>();
            else if (AppSettings.Instance.Variant == SyncTrayzorVariant.Portable)
                builder.Bind<IUpdateVariantHandler>().To<PortableUpdateVariantHandler>();
            else
                Trace.Assert(false);

            builder.Bind(typeof(IModelValidator<>)).To(typeof(FluentModelValidator<>));
            builder.Bind(typeof(IValidator<>)).ToAllImplementations();
        }

        protected override void Configure()
        {
            LogManager.Setup().LoadConfigurationFromFile();

            var pathTransformer = Container.Get<IPathTransformer>();

            // Have to set the log path before anything else
            var pathConfiguration = AppSettings.Instance.PathConfiguration;
            GlobalDiagnosticsContext.Set("LogFilePath", pathTransformer.MakeAbsolute(pathConfiguration.LogFilePath));

            AppDomain.CurrentDomain.UnhandledException += (o, e) => OnAppDomainUnhandledException(e);

            var logger = LogManager.GetCurrentClassLogger();

            var assembly = Container.Get<IAssemblyProvider>();
            logger.Info("SyncTrazor version {0} ({1}) started at {2} (.NET version: {3})", assembly.FullVersion,
                assembly.ProcessorArchitecture, assembly.Location, assembly.FrameworkDescription);
            logger.Debug("Launched with command line {Args}", Args);

            options = Container.Get<CommandLineOptionsParser>();
            if (!options.Parse(Args))
                Environment.Exit(0);

            // This needs to happen before anything which might cause the unhandled exception stuff to be shown, as that wants to know
            // where to find the log file.
            Container.Get<IApplicationPathsProvider>().Initialize(pathConfiguration);

            var client = Container.Get<IIpcCommsClientFactory>().TryCreateClient();
            if (client != null)
            {
                try
                {
                    if (options.Shutdown)
                    {
                        client.Shutdown();
                        // Give it some time to shut down
                        var elapsed = Stopwatch.StartNew();
                        while (elapsed.Elapsed < TimeSpan.FromSeconds(10) &&
                               Container.Get<IIpcCommsClientFactory>().TryCreateClient() != null)
                        {
                            Thread.Sleep(100);
                        }

                        // Wait another half-second -- it seems it can take the browser process a little longer to exit
                        Thread.Sleep(500);
                        Environment.Exit(0);
                    }

                    if (options.StartSyncthing || options.StopSyncthing)
                    {
                        if (options.StartSyncthing)
                            client.StartSyncthing();
                        else if (options.StopSyncthing)
                            client.StopSyncthing();
                        if (!options.StartMinimized)
                            client.ShowMainWindow();
                        Environment.Exit(0);
                    }

                    if (AppSettings.Instance.EnforceSingleProcessPerUser)
                    {
                        if (!options.StartMinimized)
                            client.ShowMainWindow();
                        Environment.Exit(0);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to talk to {client}: {e.Message}. Pretending that it doesn't exist...");
                }
            }

            // If we got this far, there probably isn't another instance running, and we should just shut down
            if (options.Shutdown)
            {
                Environment.Exit(0);
            }

            var configurationProvider = Container.Get<IConfigurationProvider>();
            configurationProvider.Initialize(AppSettings.Instance.DefaultUserConfiguration);
            var configuration = Container.Get<IConfigurationProvider>().Load();

            if (AppSettings.Instance.EnforceSingleProcessPerUser)
            {
                Container.Get<IIpcCommsServer>().StartServer();
            }

            // Has to be done before the VMs are fetched from the container
            if (options.Culture != null)
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(options.Culture);
            else if (!configuration.UseComputerCulture)
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            // WPF ignores the current culture by default - so we have to force it
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    XmlLanguage.GetLanguage(Thread.CurrentThread.CurrentCulture.IetfLanguageTag)));

            var autostartProvider = Container.Get<IAutostartProvider>();
#if DEBUG
            autostartProvider.IsEnabled = false;
#endif

            // If it's not in portable mode, and if we had to create config (i.e. it's the first start ever), then enable autostart
            if (autostartProvider.CanWrite && AppSettings.Instance.EnableAutostartOnFirstStart &&
                configurationProvider.HadToCreateConfiguration)
                autostartProvider.SetAutoStart(new AutostartConfiguration()
                    { AutoStart = true, StartMinimized = true });

            // Needs to be done before ConfigurationApplicator is run
            Container.Get<IApplicationWindowState>().Setup(RootViewModel);

            Container.Get<ConfigurationApplicator>().ApplyConfiguration();

            Container.Get<MemoryUsageLogger>().Enabled = true;

            // Handles Restart Manager requests - sent by the installer. We need to shutdown syncthing in this case
            Application.SessionEnding += (o, e) =>
            {
                LogManager.GetCurrentClassLogger().Info("Shutting down: {0}", e.ReasonSessionEnding);
                var manager = Container.Get<ISyncthingManager>();
                manager.StopAndWaitAsync().Wait(2000);
                manager.Kill();
            };

            MessageBoxViewModel.ButtonLabels = new Dictionary<MessageBoxResult, string>()
            {
                { MessageBoxResult.Cancel, Resources.Generic_Dialog_Cancel },
                { MessageBoxResult.No, Resources.Generic_Dialog_No },
                { MessageBoxResult.OK, Resources.Generic_Dialog_OK },
                { MessageBoxResult.Yes, Resources.Generic_Dialog_Yes },
            };

            MessageBoxViewModel.DefaultFlowDirection = Localizer.FlowDirection;

            RecycleBinDeleter.Logger = s => LogManager.GetLogger(typeof(RecycleBinDeleter).FullName).Error(s);

            // Workaround for Intel Xe processors, which mess up CefSharp unless we disable hardware
            // rendering for WPF. See #606.
            if (configuration.DisableHardwareRendering)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
        }

        protected override void Launch()
        {
            if (options.StartMinimized)
                Container.Get<INotifyIconManager>().EnsureIconVisible();
            else
                base.Launch();
        }

        protected override void OnLaunch()
        {
            Container.Get<IApplicationState>().ApplicationStarted();

            var config = Container.Get<IConfigurationProvider>().Load();
            if (options.StartSyncthing || (config.StartSyncthingAutomatically && !options.StopSyncthing))
                RootViewModel.Start();

            // If we've just been upgraded, and we're minimized, show a bit of toast explaining the fact
            if (options.StartMinimized && Container.Get<IConfigurationProvider>().WasUpgraded)
            {
                var updatedVm = Container.Get<NewVersionInstalledToastViewModel>();
                updatedVm.Version = Container.Get<IAssemblyProvider>().Version;
                Container.Get<INotifyIconManager>().ShowBalloonAsync(updatedVm, timeout: 5000);
            }

            var logger = LogManager.GetCurrentClassLogger();
            logger.Debug("Cleaning up config folder path");
            Container.Get<ConfigFolderCleaner>().Clean();

            Container.Get<IConflictFileWatcher>();
        }

        private void OnAppDomainUnhandledException(UnhandledExceptionEventArgs e)
        {
            // Testing has indicated that this and OnUnhandledException won't be called at the same time
            var logger = LogManager.GetCurrentClassLogger();
            logger.Error(e.ExceptionObject as Exception,
                $"An unhandled AppDomain exception occurred. Terminating: {e.IsTerminating}");
        }

        protected override void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = LogManager.GetCurrentClassLogger();
            logger.Error(e.Exception, "An unhandled exception occurred");
            if (e.Exception is ReflectionTypeLoadException typeLoadException)
            {
                logger.Error("Loader exceptions:");
                foreach (var ex in typeLoadException.LoaderExceptions)
                {
                    logger.Error(ex);
                }
            }

            if (Container == null)
            {
                // This happened very early on... Not much we can do.
                MessageBox.Show(
                    $"An unexpected exception occurred during startup:\n\n{e.Exception}",
                    "An unexpected exception occurred",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            // It's nicer if we try stopping the syncthing process, but if we can't, carry on
            try
            {
                Container.Get<ISyncthingManager>().StopAsync();
            }
            catch
            {
                // ignored
            }

            // If we're shutting down, we're not going to be able to display an error dialog....
            // We've logged it. Nothing else we can do.
            if (exiting)
                return;

            try
            {
                var windowManager = Container.Get<IWindowManager>();

                if (e.Exception is CouldNotFindSyncthingException couldNotFindSyncthingException)
                {
                    var msg =
                        $"Could not find syncthing.exe at {couldNotFindSyncthingException.SyncthingPath}\n\nIf you deleted it manually, put it back. If an over-enthsiastic " +
                        "antivirus program quarantined it, restore it. If all else fails, download syncthing.exe from https://github.com/syncthing/syncthing/releases the put it " +
                        "in this location.\n\nSyncTrayzor will now close.";
                    windowManager.ShowMessageBox(msg, "Configuration Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Don't "crash"
                    e.Handled = true;
                    Application.Shutdown();
                    return;
                }

                if (e.Exception is BadConfigurationException configurationException)
                {
                    var inner = configurationException.InnerException.Message;
                    if (configurationException.InnerException.InnerException != null)
                        inner += ": " + configurationException.InnerException.InnerException.Message;

                    var msg = String.Format("Failed to parse the configuration file at {0}.\n\n{1}\n\n" +
                                            "If you manually downgraded SyncTrayzor, note that this is not supported.\n\n" +
                                            "Please attempt to fix {0} by hand. If unsuccessful, please delete {0} and let SyncTrayzor re-create it.\n\n" +
                                            "SyncTrayzor will now close.", configurationException.ConfigurationFilePath,
                        inner);
                    windowManager.ShowMessageBox(msg, "Configuration Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Don't "crash"
                    e.Handled = true;
                    Application.Shutdown();
                    return;
                }

                if (e.Exception is CouldNotSaveConfigurationExeption couldNotSaveConfigurationException)
                {
                    var msg = $"Could not save configuration file.\n\n" +
                              $"{couldNotSaveConfigurationException.InnerException.GetType().Name}: {couldNotSaveConfigurationException.InnerException.Message}.\n\n" +
                              $"Please close any other programs which may be using this file.";

                    windowManager.ShowMessageBox(msg, "Configuration Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Don't "crash"
                    e.Handled = true;
                    Application.Shutdown();
                    return;
                }

                var vm = Container.Get<UnhandledExceptionViewModel>();
                vm.Exception = e.Exception;
                windowManager.ShowDialog(vm);
            }
            catch (Exception exception)
            {
                // Don't re-throw. Nasty stuff happens if we throw an exception while trying to handle an unhandled exception
                // For starters, the event log shows the wrong exception - this one, instead of the root cause
                logger.Error(exception, "Unhandled exception while trying to display unhandled exception window");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            exiting = true;

            // Try and be nice and close SyncTrayzor gracefully, before the Dispose call on SyncthingProcessRunning kills it dead
            Container.Get<ISyncthingManager>().StopAndWaitAsync().Wait(2000);
        }

        public override void Dispose()
        {
            // Probably need to make Stylet to this...
            ScreenExtensions.TryDispose(RootViewModel);
            base.Dispose();
        }
    }
}