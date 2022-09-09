using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class MeadowExecutionCommand : ProcessExecutionCommand
    {
        // Adrian: Task because it's been assigned in a non-async method
        // i.e. it's a task to avoid awaiting the assignment (lazy but harmless)
        public Task<List<string>> ReferencedAssemblies { get; set; }

        public FilePath OutputDirectory { get; set; }

        OutputLogger logger;
        MeadowDeviceHelper meadow = null;
        DebuggingServer meadowDebugServer = null;

        public MeadowExecutionCommand() :  base()
        {
            logger = new OutputLogger();
        }

        public async Task DeployApp(int debugPort, CancellationToken cancellationToken)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            cleanedup = false;
            meadow?.Dispose();

            var target = this.Target as MeadowDeviceExecutionTarget;
            var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger);

            meadow = new MeadowDeviceHelper(device, device.Logger);

            var appPathDll = System.IO.Path.Combine (OutputDirectory, "App.dll");

            if (meadow.DeviceAndAppVersionsMatch(appPathDll))
            {
                //wrap this is a try/catch so it doesn't crash if the developer is offline
                try {
                    string osVersion = await meadow.GetOSVersion(TimeSpan.FromSeconds(30), cancellationToken)
                        .ConfigureAwait(false);

                    await new DownloadManager (logger).DownloadLatestAsync(osVersion)
                        .ConfigureAwait(false);
                }
                catch
                {
                    Console.WriteLine("OS download failed, make sure you have an active internet connection");
                }

                var configuration = IdeApp.Workspace.ActiveConfiguration;

                var isScs = configuration is SolutionConfigurationSelector;
                var isDebug = (configuration as SolutionConfigurationSelector)?.Id == "Debug";

                var includePdbs = (isScs && isDebug && debugPort > 1000);

                await meadow.DeployAppAsync(appPathDll, includePdbs, cancellationToken);

                if (includePdbs)
                {
                    meadowDebugServer = await meadow.StartDebuggingSessionAsync(debugPort, cancellationToken);
                }
                else
                {
                    // sleep until cancel since this is a normal deploy without debug
                    while (!cancellationToken.IsCancellationRequested)
                        await Task.Delay(1000);

                    Cleanup();
                }
            }
            else {
                return;
            }
        }

        bool cleanedup = true;
        public void Cleanup()
        {
            if (cleanedup)
                return;

            try {
                meadowDebugServer?.StopListeningAsync();
            } catch
            {
            }

            try {
                meadowDebugServer?.Dispose();
            }
            finally {
                meadowDebugServer = null;
            }

            try {
                meadow?.Dispose();
            }
            finally {
                meadow = null;
            }

            if (!cleanedup)
                _ = DeploymentTargetsManager.StartPollingForDevices();

            cleanedup = true;
        }
    }
}