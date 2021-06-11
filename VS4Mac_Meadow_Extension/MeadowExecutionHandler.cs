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

        object _lock = new object();



        public ProcessAsyncOperation Execute(ExecutionCommand command, OperationConsole console)
        {
            var cmd = command as MeadowExecutionCommand;

            var cts = new CancellationTokenSource();
            var deployTask = DeployApp(cmd.Target as MeadowDeviceExecutionTarget, cmd.OutputDirectory, cts);

            return new ProcessAsyncOperation(deployTask, cts);
        }

        //https://stackoverflow.com/questions/29798243/how-to-write-to-the-tool-output-pad-from-within-a-monodevelop-add-in
        async Task DeployApp(MeadowDeviceExecutionTarget target, string folder, CancellationTokenSource cts)
        {
            await Task.Run(() =>
            {
                DeploymentTargetsManager.StopPollingForDevices();

                if(monitor == null)
                {
                    ProgressMonitor toolMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetToolOutputProgressMonitor(true, cts);
                    ProgressMonitor outMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("Meadow", IconId.Null, true);

                    monitor = new AggregatedProgressMonitor(toolMonitor, new ProgressMonitor[] { outMonitor });
                }
            });

            monitor?.BeginTask("Deploying to Meadow ...", 1);

            try
            {
                var meadow = target.MeadowDevice;

                if(await InitializeMeadowDevice(meadow, monitor, cts) == false)
                {
                    throw new Exception("Failed to initialize Meadow");
                }

                //run linker
                //ManualLink.LinkApp(folder);
                //await Task.Delay(50);//pause to release file handle

                var linkFolder = Path.Combine(folder, ManualLink.LinkFolder);

                var assets = GetLocalAssets(monitor, cts, folder);
                //var linkedFiles = GetNonSystemFiles(GetLocalAppFiles(monitor, cts, linkFolder));
                var unlinkedFiles = GetLocalAppFiles(monitor, cts, folder); //GetSystemFiles to isolate

                await meadow.MonoDisableAsync(cts.Token);

                var meadowFiles = await GetFilesOnDevice(meadow, monitor, cts);


                var allFiles = new Dictionary<string, uint>();

                assets.ToList().ForEach(x => allFiles.Add(x.Key, x.Value));
                unlinkedFiles.ToList().ForEach(x => allFiles.Add(x.Key, x.Value));

                await DeleteUnusedFiles(meadow, monitor, cts, meadowFiles, allFiles);

                //deploy app
              //  await DeployFilesWithCrcCheck(meadow, monitor, cts, linkFolder, meadowFiles, appFiles);
              //  await DeployFilesWithGuidCheck(meadow, monitor, cts, linkFolder, meadowFiles, appFiles);
                await DeployFilesWithCrcCheck(meadow, monitor, cts, folder, meadowFiles, allFiles);

                await meadow.MonoEnableAsync(cts.Token);

                monitor.ReportSuccess("Resetting Meadow and starting app");
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
        }

        async Task<bool> InitializeMeadowDevice(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) { return true; }

            await monitor.Log.WriteLineAsync("Initializing Meadow");

            if (meadow == null)
            {
                monitor.ErrorLog.WriteLine("Can't read Meadow device");
                return false;
            }

            if(await meadow.InitializeAsync() == false)
            {
                monitor.ErrorLog.WriteLine("Couldn't initialize serial port");
                return false;
            }
            return true;
        }

        async Task<IDictionary<string, uint>> GetFilesOnDevice(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) { return new Dictionary<string, uint>(); }

            await monitor.Log.WriteLineAsync("Checking files on device (may take several seconds)");

            var meadowFiles = await meadow.GetFilesAndCrcsAsync(30000);

            foreach (var f in meadowFiles)
            {
                if (cts.IsCancellationRequested) break;
                await monitor.Log.WriteLineAsync($"Found {f}").ConfigureAwait(false);
            }

            return meadowFiles;
        }

        void RenameAppLib(string appFolder)
        {
            string dllName = Path.Combine(appFolder, "App.dll");
            string exeName = Path.Combine(appFolder, "App.exe");
            if (File.Exists(dllName))
            {
                if (File.Exists(exeName))
                    File.Delete(exeName);

                File.Copy(dllName, exeName);
                File.Delete(dllName);
            }
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

        async Task DeleteUnusedFiles (
            MeadowSerialDevice meadow,
            ProgressMonitor monitor,
            CancellationTokenSource cts,
            IDictionary<string, uint> meadowFiles,
            IDictionary<string, uint> localFiles)
        {
            foreach(var file in meadowFiles)
            {
                if (cts.IsCancellationRequested) { break; }

                //skip - we'll delete with the dll
                /*
                if(file.Key.Contains(GUID_EXTENSION))
                {
                    var lib = file.Key.Substring(0, file.Key.Length - GUID_EXTENSION.Length);
                    if(localFiles.Keys.Contains(lib))
                    {
                    //    continue;
                    }    
                }*/
       
                if(localFiles.Contains(file) == false)
                {
                    await meadow.DeleteFileAsync(file.Key);

                    await monitor.Log.WriteLineAsync($"Deleted {file} from Meadow").ConfigureAwait(false);
                }
            }
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
        }
            
        async Task DeployFilesWithCrcCheck(
            MeadowSerialDevice meadow,
            ProgressMonitor monitor,
            CancellationTokenSource cts,
            string folder,
            IDictionary<string, uint> meadowFiles,
            IDictionary<string, uint> localFiles)
        {
            if (cts.IsCancellationRequested)
            { return; }

            foreach(var file in localFiles)
            {
                if (meadowFiles.Values.Contains(file.Value))
                {
                   // Console.WriteLine($"CRCs matched for {localFiles.files[i]}");
                    continue;
                }

                if(file.Key == "App.dll")
                {
                    await meadow.WriteFileAsync(Path.Combine(folder, file.Key), "App.exe");
                }
                else
                {
                    await meadow.WriteFileAsync(Path.Combine(folder, file.Key), file.Key);
                }

                await monitor.Log.WriteLineAsync($"Copied {file.Key} to Meadow");
                await Task.Delay(100);
            }
        }
    }
}