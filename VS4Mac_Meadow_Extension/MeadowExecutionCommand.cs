using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using Meadow.Hcom;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
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

        ILogger logger;
        IMeadowConnection meadowConnection = null;
        DebuggingServer meadowDebugServer = null;

        public MeadowExecutionCommand() : base()
        {
            logger = new OutputLogger();
        }

        public async Task DeployApp(int debugPort, bool includePdbs, CancellationToken cancellationToken)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            cleanedup = false;

            if (meadowConnection == null)
            {

                var target = Target as MeadowDeviceExecutionTarget;

                var retryCount = 0;

                Debug.WriteLine($"get_serial_connection");
            get_serial_connection:
                try
                {
                    meadowConnection = new SerialConnection(target?.Port, logger);
                }
                catch
                {
                    retryCount++;
                    if (retryCount > 10)
                    {
                        throw new Exception($"Cannot find port {target?.Port}");
                    }
                    Thread.Sleep(500);
                    goto get_serial_connection;
                }

                await meadowConnection!.WaitForMeadowAttach();
            }
            else
            {
                // TODO Maybe just set a new port, rather than create at totally new object?? meadowConnection.Name = target?.Port;
            }

            var deviceInfo = await meadowConnection?.GetDeviceInfo(cancellationToken);
            string osVersion = deviceInfo?.OsVersion;

            var fileManager = new FileManager(null);
            await fileManager.Refresh();

            var collection = fileManager.Firmware["Meadow F7"];

            //wrap this is a try/catch so it doesn't crash if the developer is offline
            try
            {
                // TODO Download OS once we have a valid MeadowCloudClient
            }
            catch (Exception e)
            {
                logger?.LogInformation($"OS download failed, make sure you have an active internet connection.{Environment.NewLine}{e.Message}");
            }

            meadowConnection!.FileWriteProgress += MeadowConnection_DeploymentProgress;
            meadowConnection!.DeviceMessageReceived += MeadowConnection_DeviceMessageReceived;

            try
            {
                var packageManager = new PackageManager(fileManager);

                await packageManager.TrimApplication(new FileInfo(Path.Combine(OutputDirectory, "App.dll")), osVersion, includePdbs, cancellationToken: cancellationToken);

                await AppManager.DeployApplication(packageManager, meadowConnection, osVersion, OutputDirectory, includePdbs, false, logger, cancellationToken);

                await meadowConnection!.WaitForMeadowAttach();
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

        private void MeadowConnection_DeviceMessageReceived(object sender, (string message, string source) e)
        {
            if (logger is OutputLogger outputLogger)
            {
                outputLogger.ReportDeviceMessage(e.message);
            }
        }

        private void MeadowConnection_DeploymentProgress(object sender, (string fileName, long completed, long total) e)
        {
            var p = (int)((e.completed / (double)e.total) * 100d);
            if (logger is OutputLogger outputLogger)
            {
                outputLogger.ReportFileProgress(e.fileName, p);
            }
        }

        bool cleanedup = true;
        public void Cleanup()
        {
            if (cleanedup)
                return;

            if (meadowDebugServer != null)
            {
                meadowDebugServer?.StopListening();
                meadowDebugServer?.Dispose();
                meadowDebugServer = null;
            }

            if (meadowConnection != null)
            {
                meadowConnection.DeviceMessageReceived -= MeadowConnection_DeviceMessageReceived;
                meadowConnection.Dispose();
                meadowConnection = null;
            }

            if (!cleanedup)
                _ = DeploymentTargetsManager.StartPollingForDevices();

            cleanedup = true;
        }
    }
}