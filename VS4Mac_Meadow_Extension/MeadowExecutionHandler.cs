using MonoDevelop.Core.Execution;
using MeadowCLI.DeviceManagement;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using MonoDevelop.Core;
using System.Collections.Generic;
using System;
using MonoDevelop.Core.ProgressMonitoring;
using MeadowCLI.Hcom;
using System.IO;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowExecutionHandler : IExecutionHandler
    {
        private ProgressMonitor outputMonitor;
        private EventHandler<MeadowMessageEventArgs> messageEventHandler;
        private MeadowSerialDevice meadowExecutionTarget;

        public bool CanExecute(ExecutionCommand command)
        {   //returning false here swaps the play button with a build button 
            return (command is MeadowExecutionCommand);
        }

        public MeadowExecutionHandler()
        {
            messageEventHandler = OnMeadowMessage;
        }

        object _lock = new object();

        private void OnMeadowMessage(object sender, MeadowMessageEventArgs args)
        {
            lock (_lock)
            {
                if (outputMonitor == null)
                {
                    outputMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor("Meadow", IconId.Null, true, true, true);
                }

                outputMonitor.Log.WriteLine(args.Message);
            }
        }

        public ProcessAsyncOperation Execute(ExecutionCommand command, OperationConsole console)
        {
            var cmd = command as MeadowExecutionCommand;

            //unsubscribe from previous device if it exists
            if(meadowExecutionTarget != null && messageEventHandler != null)
            {
                meadowExecutionTarget.OnMeadowMessage -= messageEventHandler;
            }

            //get a ref to the new execution target
            meadowExecutionTarget = (cmd.Target as MeadowDeviceExecutionTarget).MeadowDevice;

            meadowExecutionTarget.OnMeadowMessage += messageEventHandler;

            var cts = new CancellationTokenSource();
            var deployTask = DeployApp(cmd.Target as MeadowDeviceExecutionTarget, cmd.OutputDirectory, cts);

            return new ProcessAsyncOperation(deployTask, cts);
        }

        //https://stackoverflow.com/questions/29798243/how-to-write-to-the-tool-output-pad-from-within-a-monodevelop-add-in
        async Task DeployApp(MeadowDeviceExecutionTarget target, string folder, CancellationTokenSource cts)
        {
            DeploymentTargetsManager.StopPollingForDevices();

            ProgressMonitor toolMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetToolOutputProgressMonitor(true, cts);
            ProgressMonitor outMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("Meadow", IconId.Null, true);

            var monitor = new AggregatedProgressMonitor(toolMonitor, new ProgressMonitor[] { outMonitor });

            monitor.BeginTask("Deploying to Meadow ...", 1);

            try
            {
                var meadow = target.MeadowDevice;

                if(await InitializeMeadowDevice(meadow, monitor, cts) == false)
                {
                    throw new Exception("Failed to initialize Meadow");
                }

                var meadowFiles = await GetFilesOnDevice(meadow, monitor, cts);

                var localFiles = await GetLocalFiles(monitor, cts, folder);

                await DeleteUnusedFiles(meadow, monitor, cts, meadowFiles, localFiles);

                await DeployApp(meadow, monitor, cts, folder, meadowFiles, localFiles);

                await ResetMeadowAndStartMono(meadow, monitor, cts);
            }
            catch (Exception ex)
            {
                await monitor.ErrorLog.WriteLineAsync($"Error: {ex.Message}");
            }
            finally
            {
                monitor?.EndTask();
                monitor?.Dispose();

                DeploymentTargetsManager.StartPollingForDevices();
            }
        }

        async Task<bool> InitializeMeadowDevice(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) return true;

            await monitor.Log.WriteLineAsync("Initializing Meadow");

            if (meadow == null)
            {
                monitor.ErrorLog.WriteLine("Can't read Meadow device");
                return false;
            }

            meadow.Initialize(false);
            MeadowDeviceManager.MonoDisable(meadow);
            await Task.Delay(3000); //give device time to reboot

            if(meadow.Initialize() == false)
            {
                monitor.ErrorLog.WriteLine("Couldn't initialize serial port");
                return false;
            }
            return true;
        }

        async Task<(List<string> files, List<UInt32> crcs)> GetFilesOnDevice(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) { return (new List<string>(), new List<UInt32>()); }

            await monitor.Log.WriteLineAsync("Checking files on device (may take several seconds)");

            var meadowFiles = await meadow.GetFilesAndCrcs(30000);

            foreach (var f in meadowFiles.files)
            {
                if (cts.IsCancellationRequested) break;
                await monitor.Log.WriteLineAsync($"Found {f}").ConfigureAwait(false);
            }

            return meadowFiles;
        }

        async Task<(List<string> files, List<UInt32> crcs)>     GetLocalFiles(ProgressMonitor monitor, CancellationTokenSource cts, string folder)
        {
            //get list of files in folder
            var paths = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => s.EndsWith(".exe") ||
                        s.EndsWith(".dll") ||
                        s.EndsWith(".bmp") ||
                        s.EndsWith(".jpg") ||
                        s.EndsWith(".jpeg") ||
                        s.EndsWith(".txt"));

            var files = new List<string>();
            var crcs = new List<UInt32>();

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

                    Console.WriteLine($"{file} crc is {crc}");
                    files.Add(Path.GetFileName(file));
                    crcs.Add(crc);
                }
            }

            return (files, crcs);
        }

        async Task DeleteUnusedFiles (MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts,
            (List<string> files, List<UInt32> crcs) meadowFiles, (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
                return;

            foreach(var file in meadowFiles.files)
            {
                if (cts.IsCancellationRequested) { break; }
       
                if(localFiles.files.Contains(file) == false)
                {
                    await meadow.DeleteFile(file);
                    await monitor.Log.WriteLineAsync($"Removing {file}").ConfigureAwait(false);
                }
            }
        }

        async Task DeployApp(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts, string folder,
            (List<string> files, List<UInt32> crcs) meadowFiles, (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
                return;

            for(int i = 0; i < localFiles.files.Count; i++)
            {
                if (meadowFiles.crcs.Contains(localFiles.crcs[i])) continue;

                await WriteFileToMeadow(meadow, monitor, cts,
                    folder, localFiles.files[i], true);
            }
        }

        async Task WriteFileToMeadow(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts, string folder, string file, bool overwrite = false)
        {
            if (cts.IsCancellationRequested) { return; }

            if (overwrite || await meadow.IsFileOnDevice(file).ConfigureAwait(false) == false)
            {
                await monitor.Log.WriteLineAsync($"Writing {file}").ConfigureAwait(false);
                await meadow.WriteFile(file, folder).ConfigureAwait(false);
            }
        }

        async Task ResetMeadowAndStartMono(MeadowSerialDevice meadow, ProgressMonitor monitor, CancellationTokenSource cts)
        {
            if(cts.IsCancellationRequested) { return; }

            string serial = meadow.DeviceInfo.SerialNumber;

            monitor.ReportSuccess("Resetting Meadow and starting app");

            MeadowDeviceManager.MonoEnable(meadow);

            try
            {
                MeadowDeviceManager.ResetTargetMcu(meadow);
            }
            catch
            {
                //ignore for now
            }

            await Task.Delay(2500);//wait for reboot

            //reconnect serial port
            if(meadow.Initialize() == false)
            {
                //find device with matching serial //ToDo
            }
        }
    }
}