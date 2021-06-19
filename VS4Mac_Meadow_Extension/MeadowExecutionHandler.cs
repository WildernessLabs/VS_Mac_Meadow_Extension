using MonoDevelop.Core.Execution;
using System.Threading.Tasks;
using System.Threading;
using MonoDevelop.Core;
using System.Collections.Generic;
using System;
using System.IO;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Devices;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowExecutionHandler : IExecutionHandler
    {
        OutputProgressMonitor monitor;
        OutputLogger logger;

        public bool CanExecute(ExecutionCommand command)
        {   //returning false here swaps the play button with a build button

            return (command is MeadowExecutionCommand);
        }

        public MeadowExecutionHandler()
        {
           
            logger = new OutputLogger();
        }

        public ProcessAsyncOperation Execute(ExecutionCommand command, OperationConsole console)
        {
            var cmd = command as MeadowExecutionCommand;

            var cts = new CancellationTokenSource();
            var deployTask = DeployApp(cmd.Target as MeadowDeviceExecutionTarget, cmd.OutputDirectory, cts);

            return new ProcessAsyncOperation(deployTask, cts);

           // return DebuggingService.GetExecutionHandler().Execute(command, console);
        }

        MeadowDeviceHelper meadow;

        //https://stackoverflow.com/questions/29798243/how-to-write-to-the-tool-output-pad-from-within-a-monodevelop-add-in
        async Task DeployApp(MeadowDeviceExecutionTarget target, string folder, CancellationTokenSource cts)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            meadow?.Dispose();

            try
            {
                var device = await MeadowDeviceManager.GetMeadowForSerialPort(target.Port, logger: logger);

                meadow = new MeadowDeviceHelper(device, device.Logger);

                await meadow.MonoDisableAsync(cts.Token);

                var fileNameExe = Path.Combine(folder, "App.dll");

                await meadow.DeployAppAsync(fileNameExe, true, cts.Token);

                await meadow.MonoEnableAsync(cts.Token);

                //sit here and wait for cancellation
                while (true)
                {
                    await Task.Delay(1000);
                    if(cts.Token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await monitor?.ErrorLog.WriteLineAsync($"Error: {ex.Message}");
            }
            finally
            {
                meadow?.Dispose();

                //fire and forget
                _ = DeploymentTargetsManager.StartPollingForDevices();
            }

            return;
        } 

        IDictionary<string, uint> GetLocalAppFiles(
            ProgressMonitor monitor,
            CancellationTokenSource cts,
            string appFolder)
        {
            var files = new Dictionary<string, uint>();

            //crawl dependences
            //var paths = Directory.EnumerateFiles(appFolder, "*.*", SearchOption.TopDirectoryOnly);

            List<string> dependences;
         //   RenameAppLib(appFolder);
        
            dependences = AssemblyManager.GetDependencies("App.dll", appFolder);
            dependences.Add("App.dll");

            foreach (var file in dependences)
            {
                if (cts.IsCancellationRequested) { break; }

                using (FileStream fs = File.Open(Path.Combine(appFolder, file), FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    fs.Read(bytes, 0, len);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

                  //  Console.WriteLine($"{file} crc is {crc}");
                    files.Add(Path.GetFileName(file), crc);
                }
            }

            return files;
        }

        /*
        async Task DeployFilesWithGuidCheck(
            MeadowSerialDevice meadow,
            ProgressMonitor monitor,
            CancellationTokenSource cts,
            string folder,
            (List<string> files, List<UInt32> crcs) meadowFiles,
            (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
            { return; }

            var weaver = new WeaverCRC();

            for (int i = 0; i < localFiles.files.Count; i++)
            {
                var guidFileName = localFiles.files[i] + GUID_EXTENSION;
                string guidOnMeadow = string.Empty;

                if(meadowFiles.files.Contains(guidFileName))
                {
                    //ToDo
                   // guidOnMeadow = await meadow.GetInitialFileData(guidFileName);
                    await Task.Delay(100);
                }

                //calc guid 
                var guidLocal = weaver.GetCrcGuid(Path.Combine(folder, localFiles.files[i])).ToString();

                if(guidLocal == guidOnMeadow)
                {
                    continue;
                }

                Console.WriteLine($"Guids didn't match for {localFiles.files[i]}");
                await meadow.WriteFileAsync(Path.Combine(folder, localFiles.files[i]), localFiles.files[i]);

                await Task.Delay(250);

                //need to write new Guid file
                
                var guidFilePath = Path.Combine(folder, guidFileName);
                File.WriteAllText(guidFilePath, guidLocal);

                await meadow.WriteFileAsync(guidFilePath, guidFileName);

                await Task.Delay(250);
            }
        } */
    }
}