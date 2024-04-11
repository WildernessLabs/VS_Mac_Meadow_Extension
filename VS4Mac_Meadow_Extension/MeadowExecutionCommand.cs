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
using Microsoft.Extensions.Logging;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class MeadowExecutionCommand : ProcessExecutionCommand
    {
        public MeadowExecutionCommand() : base()
        {
            logger = new OutputLogger();
        }

        // Adrian: Task because it's been assigned in a non-async method
        // i.e. it's a task to avoid awaiting the assignment (lazy but harmless)
        public Task<List<string>> ReferencedAssemblies { get; set; }

        public FilePath OutputDirectory { get; set; }

        ILogger logger;
        MeadowDeviceHelper meadow = null;
        DebuggingServer meadowDebugServer = null;

        public async Task DeployApp(int debugPort, CancellationToken cancellationToken)
        {
            var configuration = IdeApp.Workspace.ActiveConfiguration;

            bool isDebugging = configuration is SolutionConfigurationSelector isScs
                && isScs?.Id == "Debug"
                && debugPort > 1000;

            try
            {
                DeploymentTargetsManager.StopPollingForDevices();

                cleanedup = false;
                meadow?.Dispose();

                var target = this.Target as MeadowDeviceExecutionTarget;
                var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger)
                    .ConfigureAwait(false);

                meadow = new MeadowDeviceHelper(device, device.Logger);

                //wrap this is a try/catch so it doesn't crash if the developer is offline
                try
                {
                    string osVersion = await meadow.GetOSVersion(TimeSpan.FromSeconds(30), cancellationToken);

                    await new DownloadManager(logger).DownloadOsBinaries(osVersion);
                }
                catch (Exception e)
                {
                    logger.LogError($"OS download failed, make sure you have an active internet connection.{Environment.NewLine}Error:{e.Message}");
                }

                var fileNameExe = System.IO.Path.Combine(OutputDirectory, "App.dll");

                await meadow.DeployApp(fileNameExe, isDebugging, cancellationToken);
            }
            finally
            {
                var running = await meadow.GetMonoRunState(cancellationToken);
                if (!running)
                {
                    await meadow?.MonoEnable(true, cancellationToken);
                }
            }

            if (isDebugging)
            {
                meadowDebugServer = await meadow.StartDebuggingSession(debugPort, cancellationToken);
            }
            else
            {
                // sleep until cancel since this is a normal deploy without debug
                while (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(1000, cancellationToken);

                Cleanup();
            }
        }

        bool cleanedup = true;
        public void Cleanup()
        {
            if (cleanedup)
                return;

            meadowDebugServer?.StopListening();
            meadowDebugServer?.Dispose();
            meadowDebugServer = null;

            meadow?.Dispose();

            if (!cleanedup)
                _ = DeploymentTargetsManager.StartPollingForDevices();

            cleanedup = true;
        }
    }
}