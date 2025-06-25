using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Hcom;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;



namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class MeadowExecutionCommand : ProcessExecutionCommand
    {
        // Adrian: Task because it's been assigned in a non-async method
        // i.e. it's a task to avoid awaiting the assignment (lazy but harmless)
        public Task<List<string>> ReferencedAssemblies { get; set; }

        public FilePath OutputDirectory { get; set; }

        OutputLogger logger;
        public OutputLogger Logger => logger;

        IMeadowConnection meadowConnection = null;
        public IMeadowConnection MeadowConnection => meadowConnection;

        private readonly SettingsManager settingsManager = new SettingsManager();
        private readonly MeadowConnectionManager connectionManager = null;

        public MeadowExecutionCommand() : base()
        {
            logger = new OutputLogger();
            this.connectionManager = new MeadowConnectionManager(settingsManager);
        }

        public async Task DeployApp(int debugPort, bool includePdbs, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            DeploymentTargetsManager.StopPollingForDevices();

            if (meadowConnection != null)
            {
                meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
                meadowConnection.DeviceMessageReceived -= MeadowConnection_DeviceMessageReceived;
                meadowConnection = null;
            }

            if (Target is MeadowDeviceExecutionTarget target)
            {
                meadowConnection = connectionManager.GetConnection(target.Port);

                meadowConnection.FileWriteProgress += MeadowConnection_DeploymentProgress;
                meadowConnection.DeviceMessageReceived += MeadowConnection_DeviceMessageReceived;

                await meadowConnection.WaitForMeadowAttach(cancellationToken);

                if (await meadowConnection.IsRuntimeEnabled(cancellationToken))
                {
                    await meadowConnection.RuntimeDisable(cancellationToken);
                }

                var deviceInfo = await meadowConnection?.GetDeviceInfo(cancellationToken);
                string osVersion = deviceInfo?.OsVersion;

                var fileManager = new FileManager(null);
                await fileManager.Refresh();

                try
                {
                    var packageManager = new PackageManager(fileManager);

                    await packageManager.TrimApplication(new FileInfo(Path.Combine(OutputDirectory, "App.dll")), osVersion, includePdbs, null, logger, cancellationToken);

                    await AppManager.DeployApplication(packageManager, meadowConnection, osVersion, OutputDirectory, includePdbs, false, logger, cancellationToken);

                    await Task.Delay(1500, cancellationToken);

                    await meadowConnection.RuntimeEnable(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError($"Deployment failed: {ex.Message}");
                    throw;
                }
                finally
                {
                    meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
                }
            }
            else
            {
                logger.LogError($"Property {nameof(Target)} is not a valid type of MeadowDeviceExecutionTarget");
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
    }
}