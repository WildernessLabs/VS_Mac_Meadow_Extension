using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using Meadow.Deployment;
using Meadow.Hcom;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Projects;



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
        IMeadowConnection meadowConnection = null;
        DebuggingServer meadowDebugServer = null;

        public async Task DeployApp(int debugPort, CancellationToken cancellationToken)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            cleanedup = false;
            // meadowConnection?.Dispose();

            /*var target = this.Target as MeadowDeviceExecutionTarget;
            var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger)
                .ConfigureAwait(false);

            meadowConnection = new MeadowDeviceHelper(device, device.Logger);*/

            if (meadowConnection == null)
            {
                Console.WriteLine("Creating SettingsManager");
                var sm = new SettingsManager();

                Console.WriteLine("Gettting Route");
                var route = sm.GetSetting(SettingsManager.PublicSettings.Route);

                Console.WriteLine($"Current Route:{route}");
                if (route == null)
                {
                    throw new Exception($"No 'route' configuration set.{Environment.NewLine}Use the `meadow config route` command. For example:{Environment.NewLine}  > meadow config route COM5");
                }

                var retryCount = 0;

                Console.WriteLine($"get_serial_connection");
                get_serial_connection:
                try
                {
                    meadowConnection = new SerialConnection(route);
                }
                catch
                {
                    retryCount++;
                    if (retryCount > 10)
                    {
                        throw new Exception($"Cannot find port {route}");
                    }
                    Thread.Sleep(500);
                    goto get_serial_connection;
                }
            }

            //wrap this is a try/catch so it doesn't crash if the developer is offline
            try
            {
                string osVersion = (await meadowConnection.GetDeviceInfo(cancellationToken)).OsVersion;

                // TODO await new DownloadManager(logger).DownloadOsBinaries(osVersion);
            }
            catch
            {
                Console.WriteLine("OS download failed, make sure you have an active internet connection");
            }

            var configuration = IdeApp.Workspace.ActiveConfiguration;

            bool includePdbs = configuration is SolutionConfigurationSelector isScs
                && isScs?.Id == "Debug"
                && debugPort > 1000;

            meadowConnection.FileWriteProgress += MeadowConnection_DeploymentProgress;

            try
            {
                await AppManager.DeployApplication(null, meadowConnection, OutputDirectory, includePdbs, false, logger, cancellationToken);
            }
            finally
            {
                meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
            }

            if (includePdbs)
            {
                meadowDebugServer = await meadowConnection?.StartDebuggingSession(debugPort, logger, cancellationToken);
            }
            else
            {
                // sleep until cancel since this is a normal deploy without debug
                while (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(1000, cancellationToken);

                Cleanup();
            }
        }

        private void MeadowConnection_DeploymentProgress(object sender, (string fileName, long completed, long total) e)
        {
            var p = (int)((e.completed / (double)e.total) * 100d);
            logger?.Report(e.fileName, p);
        }

        bool cleanedup = true;
        public void Cleanup()
        {
            if (cleanedup)
                return;

            meadowDebugServer?.StopListening();
            meadowDebugServer?.Dispose();
            meadowDebugServer = null;

            // TODO meadowConnection?.Dispose();

            if (!cleanedup)
                _ = DeploymentTargetsManager.StartPollingForDevices();

            cleanedup = true;
        }
    }
}