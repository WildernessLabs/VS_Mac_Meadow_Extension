using MonoDevelop.Core.Execution;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using MonoDevelop.Core;
using System.Collections.Generic;
using System;
using MonoDevelop.Core.ProgressMonitoring;
using System.IO;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.Tools;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Devices;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowExecutionHandler : IExecutionHandler
    {
        const string GUID_EXTENSION = ".guid";
        AggregatedProgressMonitor monitor;

        string[] SYSTEM_FILES = { "App.exe", "System.Net.dll", "System.Net.Http.dll", "mscorlib.dll", "System.dll", "System.Core.dll", "Meadow.dll" };

        public bool CanExecute(ExecutionCommand command)
        {   //returning false here swaps the play button with a build button

            return (command is MeadowExecutionCommand);
        }

        public MeadowExecutionHandler()
        {
        }


        public ProcessAsyncOperation Execute(ExecutionCommand command, OperationConsole console)
        {
            var cmd = command as MeadowExecutionCommand;

            var cts = new CancellationTokenSource();
            var deployTask = DeployApp(cmd.Target as MeadowDeviceExecutionTarget, cmd.OutputDirectory, cts);

            return new ProcessAsyncOperation(deployTask, cts);
        }

      //  IMeadowDevice meadow;

        //https://stackoverflow.com/questions/29798243/how-to-write-to-the-tool-output-pad-from-within-a-monodevelop-add-in
        async Task DeployApp(MeadowDeviceExecutionTarget target, string folder, CancellationTokenSource cts)
        {
            await Task.Run(() =>
            {
                DeploymentTargetsManager.StopPollingForDevices();

              //  if(monitor == null)
                {
             //       ProgressMonitor toolMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetToolOutputProgressMonitor(true, cts);
                    ProgressMonitor outMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("Meadow", IconId.Null, true);

                    new AggregatedProgressMonitor(outMonitor);
                   // monitor = new AggregatedProgressMonitor(toolMonitor, new ProgressMonitor[] { outMonitor });
                }
            });

            monitor?.BeginTask("Deploying to Meadow ...", 1);

            try
            {
                var meadowDH = new MeadowDeviceHelper(target.MeadowDevice, target.MeadowDevice.Logger);

                await meadowDH.MonoDisableAsync(cts.Token);

                var fileNameDll = Path.Combine(folder, "App.dll");
                var fileNameExe = Path.Combine(folder, "App.exe");

                if (File.Exists(fileNameDll))
                {
                    if (File.Exists(fileNameExe))
                    {
                        File.Delete(fileNameExe);
                    }
                    File.Copy(fileNameDll, fileNameExe);
                }

                await meadowDH.DeployAppAsync(fileNameExe, true, cts.Token);

                await meadowDH.MonoEnableAsync(cts.Token);

                target.MeadowDevice = (MeadowSerialDevice)meadowDH.MeadowDevice; //reference to keep alive

            }
            catch (Exception ex)
            {
                await monitor.ErrorLog.WriteLineAsync($"Error: {ex.Message}");
            }
            finally
            {
                monitor?.EndTask();
                monitor?.Dispose();

                //fire and forget
                var t = DeploymentTargetsManager.StartPollingForDevices();
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

        IDictionary<string, uint> GetLocalAssets(
            ProgressMonitor monitor,
            CancellationTokenSource cts,
            string assetsFolder)
        {
            //get list of files in folder
            var paths = Directory.EnumerateFiles(assetsFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => //s.EndsWith(".exe") ||
                        //s.EndsWith(".dll") ||
                        s.EndsWith(".bmp") ||
                        s.EndsWith(".jpg") ||
                        s.EndsWith(".jpeg") ||
                        s.EndsWith(".txt") ||
                        s.EndsWith(".json") ||
                        s.EndsWith(".xml") ||
                        s.EndsWith(".yml") 
                        //s.EndsWith("Meadow.Foundation.dll")
                        );

            //   var dependences = AssemblyManager.GetDependencies("App.exe" ,folder);

            var files = new Dictionary<string, uint>();

            //crawl other files (we can optimize)
            foreach (var file in paths)
            {
                if (cts.IsCancellationRequested) break;

                using (FileStream fs = File.Open(file, FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    fs.Read(bytes, 0, len);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

                    files.Add(Path.GetFileName(file), crc);
                }
            }

            return files;
        }

        (List<string> files, List<UInt32> crcs) GetSystemFiles((List<string> files, List<UInt32> crcs) files)
        {
            (List<string> files, List<UInt32> crcs) systemFiles = (new List<string>(), new List<UInt32>());

            //clean this up later with a model object and linn
            for (int i = 0; i < files.files.Count; i++)
            {
                if (SYSTEM_FILES.Contains(files.files[i]))
                {
                    systemFiles.files.Add(files.files[i]);
                    systemFiles.crcs.Add(files.crcs[i]);
                }
            }

            return systemFiles;
        }

        (List<string> files, List<UInt32> crcs) GetNonSystemFiles((List<string> files, List<UInt32> crcs) files)
        {
            (List<string> files, List<UInt32> crcs) otherFiles = (new List<string>(), new List<UInt32>());

            //clean this up later with a model object and linn
            for (int i = 0; i < files.files.Count; i++)
            {
                if (SYSTEM_FILES.Contains(files.files[i]) == false)
                {
                    otherFiles.files.Add(files.files[i]);
                    otherFiles.crcs.Add(files.crcs[i]);
                }
            }

            return otherFiles;
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