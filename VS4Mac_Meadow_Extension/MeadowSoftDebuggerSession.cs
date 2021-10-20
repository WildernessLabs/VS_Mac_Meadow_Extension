using System;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using MonoDevelop.Core.Execution;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowSoftDebuggerSession : SoftDebuggerSession
    {
        public MeadowSoftDebuggerSession()
        {
            debugCancelTokenSource = new CancellationTokenSource();
            logger = new OutputLogger();
        }

        MeadowDeviceHelper meadow = null;
        OutputProgressMonitor monitor;
        OutputLogger logger;

        CancellationTokenSource debugCancelTokenSource;

        protected override async void OnRun(DebuggerStartInfo startInfo)
        {
            if (startInfo is MeadowSoftDebuggerStartInfo meadowStartInfo)
            {
                if (meadowStartInfo.ExecutionCommand.Target is MeadowDeviceExecutionTarget meadowTarget)
                {
                    var connectArgs = meadowStartInfo.StartArgs as SoftDebuggerConnectArgs;
                    var port = connectArgs?.DebugPort ?? 0;

                    await DeployApp(meadowTarget, meadowStartInfo.ExecutionCommand.OutputDirectory, port, debugCancelTokenSource);
                }
            }

            base.OnRun(startInfo);
        }

        protected override void EndSession()
        {
            base.EndSession();

            End();
        }

        void End()
        {
            meadow?.Dispose();
            _ = DeploymentTargetsManager.StartPollingForDevices();
        }

        async Task DeployApp(MeadowDeviceExecutionTarget target, string folder, int debugPort, CancellationTokenSource cts)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            meadow?.Dispose();

            try
            {
                var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger);

                meadow = new MeadowDeviceHelper(device, device.Logger);

                var fileNameExe = System.IO.Path.Combine(folder, "App.dll");

                var debug = debugPort > 1000;

                await meadow.DeployAppAsync(fileNameExe, debug, cts.Token);

                if (debug)
                {
                    var server = await meadow.StartDebuggingSessionAsync(debugPort, cts.Token);

                    await monitor.Log.WriteLineAsync($"Started Debug Server: {server.LocalEndpoint.Address}:{server.LocalEndpoint.Port}");
                }
            }
            catch (Exception ex)
            {
                await monitor?.ErrorLog.WriteLineAsync($"Error: {ex.Message}");
            }
            finally
            {
                End();
            }

            return;
        }

    }
}