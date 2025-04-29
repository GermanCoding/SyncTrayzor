﻿using NLog;
using SyncTrayzor.Syncthing;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Services.Ipc
{
    public interface IIpcCommsServer : IDisposable
    {
        void StartServer();
        void StopServer();
    }

    public class IpcCommsServer : IIpcCommsServer
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly ISyncthingManager syncthingManager;
        private readonly IApplicationState applicationState;
        private readonly IApplicationWindowState windowState;

        private CancellationTokenSource cts;

        public string PipeName =>  $"SyncTrayzor-{Process.GetCurrentProcess().Id}";

        public IpcCommsServer(ISyncthingManager syncthingManager, IApplicationState applicationState, IApplicationWindowState windowState)
        {
            this.syncthingManager = syncthingManager;
            this.applicationState = applicationState;
            this.windowState = windowState;
        }

        public void StartServer()
        {
            if (cts != null)
                return;

            cts = new CancellationTokenSource();
            StartInternal(cts.Token);
        }

        public void StopServer()
        {
            if (cts != null)
                cts.Cancel();
        }

        private async void StartInternal(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[256];
            var commandBuilder = new StringBuilder();

            try
            {
                var serverStream = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 0, 0);

                using (cancellationToken.Register(() => serverStream.Close()))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Factory.FromAsync(serverStream.BeginWaitForConnection, serverStream.EndWaitForConnection, TaskCreationOptions.None);

                        int read = await serverStream.ReadAsync(buffer, 0, buffer.Length);
                        commandBuilder.Append(Encoding.ASCII.GetString(buffer, 0, read));

                        while (!serverStream.IsMessageComplete)
                        {
                            read = serverStream.Read(buffer, 0, buffer.Length);
                            commandBuilder.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        }

                        var response = HandleReceivedCommand(commandBuilder.ToString());
                        var responseBytes = Encoding.ASCII.GetBytes(response);

                        try
                        {
                            serverStream.Write(responseBytes, 0, responseBytes.Length);

                            serverStream.WaitForPipeDrain();
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, "Unable to write response to pipe");
                        }

                        serverStream.Disconnect();

                        commandBuilder.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Pipe server threw an exception");
            }
        }

        private string HandleReceivedCommand(string command)
        {
            switch (command)
            {
                case "Shutdown":
                    Shutdown();
                    return "OK";

                case "ShowMainWindow":
                    ShowMainWindow();
                    return "OK";

                case "StartSyncthing":
                    StartSyncthing();
                    return "OK";

                case "StopSyncthing":
                    StopSyncthing();
                    return "OK";

                default:
                    return "UnknownCommand";
            }
        }

        private void Shutdown()
        {
            applicationState.Shutdown();
        }

        private void ShowMainWindow()
        {
            windowState.EnsureInForeground();
        }

        private async void StartSyncthing()
        {
            if (syncthingManager.State == SyncthingState.Stopped)
            {
                logger.Debug("IPC client requested Syncthing start, so starting");

                try
                {
                    await syncthingManager.StartAsync();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to start syncthing");
                }
            }
            else
            {
                logger.Debug($"IPC client requested Syncthing start, but its state is {syncthingManager.State}, so not starting");
            }
        }

        private async void StopSyncthing()
        {
            if (syncthingManager.State == SyncthingState.Running)
            {
                logger.Debug("IPC client requested Syncthing stop, so stopping");

                try
                {
                    await syncthingManager.StopAsync();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to stop Syncthing");
                }
            }
            else
            {
                logger.Debug($"IPC client requested Syncthing stop, but its state is {syncthingManager.State}, so not stopping");
            }
        }

        public void Dispose()
        {
            StopServer();
        }
    }
}
