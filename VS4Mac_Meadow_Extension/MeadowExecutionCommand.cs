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

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class MeadowExecutionCommand : ProcessExecutionCommand
    {
        public MeadowExecutionCommand() :  base()
        {
            logger = new OutputLogger();
        }

        // Adrian: Task because it's been assigned in a non-async method
        // i.e. it's a task to avoid awaiting the assignment (lazy but harmless)
        public Task<List<string>> ReferencedAssemblies { get; set; }

        public FilePath OutputDirectory { get; set; }

        OutputLogger logger;
        MeadowDeviceHelper meadow = null;
        DebuggingServer meadowDebugServer = null;

        static ProgressMonitor statusMonitor;
        const int TOTAL_PROGRESS = 100;
        int nextProgress = 0;
        const int PROGRESS_INCREMENT = 5;

        public async Task DeployApp(int debugPort, CancellationToken cancellationToken)
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
            catch
            {
                Console.WriteLine("OS download failed, make sure you have an active internet connection");
            }

            var fileNameExe = System.IO.Path.Combine(OutputDirectory, "App.dll");

            var configuration = IdeApp.Workspace.ActiveConfiguration;

            bool includePdbs = configuration is SolutionConfigurationSelector isScs
                && isScs?.Id == "Debug"
                && debugPort > 1000;

            if (meadow is MeadowLocalDevice mld)
            {
                mld.MeadowProgress.ProgressChanged += MeadowProgress_ProgressChanged;
            }

            await meadow.DeployApp(fileNameExe, includePdbs, cancellationToken);

            if (includePdbs)
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

        private void MeadowProgress_ProgressChanged(object sender, CLI.Core.Progress.ProgressEventArgs e)
        {
            // updateon UI thread.
            Runtime.RunInMainThread(() =>
            {
                if (statusMonitor is null)
                    statusMonitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("File Transferring", IconId.Null, false);

                if (e.Value == 0)
                {
                    statusMonitor?.BeginTask("File Transferrring", TOTAL_PROGRESS);
                    nextProgress = 0;
                }
                else if (e.Value > 0 && e.Value < TOTAL_PROGRESS)
                {
                    statusMonitor?.Step(nextProgress);
                    nextProgress += PROGRESS_INCREMENT;
                }
                else if (e.Value >= TOTAL_PROGRESS)
                {
                    statusMonitor.EndTask();
                    nextProgress = 0;
                }
            });
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