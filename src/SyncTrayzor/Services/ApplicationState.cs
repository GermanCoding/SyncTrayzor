using System;
using System.Windows;

using Microsoft.Win32;

using SyncTrayzor.Pages;

namespace SyncTrayzor.Services
{
    public interface IApplicationState
    {
        event EventHandler Startup;
        event EventHandler ResumeFromSleep;

        void ApplicationStarted();
        ShutdownMode ShutdownMode { get; set; }
        bool HasMainWindow { get; }
        object FindResource(object resourceKey);
        void Shutdown();
    }

    public class ApplicationState : IApplicationState
    {
        private readonly Application application;

        public event EventHandler Startup;
        public event EventHandler ResumeFromSleep;

        public ApplicationState(Application application)
        {
            this.application = application;

            SystemEvents.PowerModeChanged += (o, e) =>
            {
                if (e.Mode == PowerModes.Resume)
                    OnResumeFromSleep();
            };
        }

        public ShutdownMode ShutdownMode
        {
            get => application.ShutdownMode;
            set
            {
                // This will fail if we're shutting down
                try
                {
                    application.ShutdownMode = value;
                }
                catch (InvalidOperationException) { }
            }
        }

        public bool HasMainWindow => application.MainWindow is ShellView;

        public object FindResource(object resourceKey)
        {
            return application.FindResource(resourceKey);
        }

        public void Shutdown()
        {
            application.Shutdown();
        }

        public void ApplicationStarted()
        {
            Startup?.Invoke(this, EventArgs.Empty);
        }

        private void OnResumeFromSleep()
        {
            ResumeFromSleep?.Invoke(this, EventArgs.Empty);
        }
    }
}
