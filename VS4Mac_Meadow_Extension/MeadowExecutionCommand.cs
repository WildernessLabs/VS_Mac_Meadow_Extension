using System.Collections.Generic;
using System.Threading.Tasks;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core;
using System.Threading;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.DeviceManagement;
using System;
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

        public async Task DeployApp(int debugPort, CancellationToken cancellationToken)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            meadow?.Dispose();

            var target = this.Target as MeadowDeviceExecutionTarget;
            var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger);

            meadow = new MeadowDeviceHelper(device, device.Logger);

            var fileNameExe = System.IO.Path.Combine(OutputDirectory, "App.dll");

            var debug = debugPort > 1000;

            await meadow.DeployAppAsync(fileNameExe, debug, cancellationToken);

            if (debug)
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

            return;
        }

        public void Cleanup()
        {
            meadowDebugServer?.StopListeningAsync();
            meadowDebugServer?.Dispose();

            meadow?.Dispose();

            _ = DeploymentTargetsManager.StartPollingForDevices();        
        }
    }
}