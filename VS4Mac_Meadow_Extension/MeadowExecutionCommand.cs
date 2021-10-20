using System.Collections.Generic;
using System.Threading.Tasks;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core;
using System.Threading;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.DeviceManagement;
using System;

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

        OutputProgressMonitor monitor;
        OutputLogger logger;
        MeadowDeviceHelper meadow = null;

        public async Task DeployApp(int debugPort, CancellationToken cancellationToken)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            meadow?.Dispose();

            var target = this.Target as MeadowDeviceExecutionTarget;

            try
            {
                var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger);

                meadow = new MeadowDeviceHelper(device, device.Logger);

                var fileNameExe = System.IO.Path.Combine(OutputDirectory, "App.dll");

                var debug = debugPort > 1000;

                await meadow.DeployAppAsync(fileNameExe, debug, cancellationToken);

                if (debug)
                {
                    var server = await meadow.StartDebuggingSessionAsync(debugPort, cancellationToken);

                    await monitor.Log.WriteLineAsync($"Started Debug Server: {server.LocalEndpoint.Address}:{server.LocalEndpoint.Port}");
                }
            }
            catch (Exception ex)
            {
                await monitor?.ErrorLog.WriteLineAsync($"Error: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }

            return;
        }

        public void Cleanup()
        {
            meadow?.Dispose();
            _ = DeploymentTargetsManager.StartPollingForDevices();        
        }
    }
}