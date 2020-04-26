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
using Meadow.CLI.DeviceManagement;
using System.Diagnostics;
using Meadow.Sdks.IdeExtensions.Vs4Mac.Gui;
using Gtk;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowExecutionHandler : IExecutionHandler
    {
       
     //   private MeadowDeviceExecutionTarget meadowExecutionTarget;
        


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
            
            //get a ref to the new execution target
            var target = (cmd.Target as MeadowDeviceExecutionTarget);
            Task deployTask;
            var cts = new CancellationTokenSource();


            //If there is more than one meadow, then open a list box    
            if (MeadowProject.DeploymentTargetsManager.Count > 1)
            {
                var tcs = new TaskCompletionSource<ResponseType>();
                var meadowList = new MeadowSelect(MeadowProject.DeploymentTargetsManager);

                meadowList.Response += (o, args) =>
                {
                    tcs.SetResult(args.ResponseId);
                };
                meadowList.Run(); //Blocks                
                target = meadowList.GetSelection();
                meadowList.HideAll();
                meadowList.Destroy();
                if (tcs.Task.Result == ResponseType.Ok && target != null)
                {
                    deployTask = DeployApp(target, cmd.OutputDirectory, cts);
                    return new ProcessAsyncOperation(deployTask, cts);
                }
                else
                {
                    return null;
                }
            }
            
            deployTask = DeployApp(target, cmd.OutputDirectory, cts);
            return new ProcessAsyncOperation(deployTask, cts);
        }

        

        //https://stackoverflow.com/questions/29798243/how-to-write-to-the-tool-output-pad-from-within-a-monodevelop-add-in
        async Task DeployApp(MeadowDeviceExecutionTarget target, string folder, CancellationTokenSource cts)
        {

            //ProgressMonitor toolMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetToolOutputProgressMonitor(true, cts);
            //ProgressMonitor outMonitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("Meadow", IconId.Null, true);

            //var monitor = new AggregatedProgressMonitor(toolMonitor, new ProgressMonitor[] { outMonitor });

            //target.meadowPad.Window.Visible = true;
            target.meadowPad.Window.Activate(true);
            target.meadowPad.Window.Sticky = true;
            
            

            target.Write("Deploying to Meadow ...\n");

            try
            {
                var meadow = target.meadowSerialDevice;

                if (await InitializeMeadowDevice(target, cts) == false)
                {
                    throw new Exception($"Failed to initialize Meadow {meadow.connection.ToString()}");
                }

                var meadowFiles = await GetFilesOnDevice(target, cts);

                var localFiles = await GetLocalFiles(target, cts, folder);

                await DeleteUnusedFiles(target, cts, meadowFiles, localFiles);

                await DeployApp(target, cts, folder, meadowFiles, localFiles);

                await ResetMeadowAndStartMono(target, cts);
            }
            catch (Exception ex)
            {
                target.WriteError($"Error: {ex.Message}\n");
            }
        }

        async Task<bool> InitializeMeadowDevice(MeadowDeviceExecutionTarget target, CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) return true;

            target.Write($"Initializing Meadow {target.meadowSerialDevice.connection.ToString()}\n");

            if (target.meadowSerialDevice == null)
            {
                target.WriteError("Can't read Meadow device\n");
                return false;
            }

            Console.WriteLine("MonoDisable");
            MeadowDeviceManager.MonoDisable(target.meadowSerialDevice);

            if (!(await target.meadowSerialDevice.AwaitStatus(5000, MeadowCLI.DeviceManagement.MeadowSerialDevice.DeviceStatus.PortOpen)).HasValue)
            {
                target.Write("The Meadow has failed to restart.\n");
                return false;
            }
            return true;
        }




        async Task<(List<string> files, List<UInt32> crcs)> GetFilesOnDevice(MeadowDeviceExecutionTarget target , CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) { return (new List<string>(), new List<UInt32>()); }

            target.Write("Checking files on device (may take several seconds)\n");

            var meadowFiles = await target.meadowSerialDevice.GetFilesAndCrcs(30000);

            foreach (var f in meadowFiles.files)
            {
                if (cts.IsCancellationRequested) break;
                target.Write($"Found {f}\n");
            }

            return meadowFiles;
        }

        async Task<(List<string> files, List<UInt32> crcs)> GetLocalFiles(MeadowDeviceExecutionTarget target,CancellationTokenSource cts, string folder)
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

        async Task DeleteUnusedFiles (MeadowDeviceExecutionTarget target , CancellationTokenSource cts,
            (List<string> files, List<UInt32> crcs) meadowFiles, (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
                return;

            foreach(var file in meadowFiles.files)
            {
                if (cts.IsCancellationRequested) { break; }
       
                if(localFiles.files.Contains(file) == false)
                {
                    await target.meadowSerialDevice.DeleteFile(file);
                    target.Write($"Removing {file}\n");
                }
            }
        }

        async Task DeployApp(MeadowDeviceExecutionTarget target , CancellationTokenSource cts, string folder,
            (List<string> files, List<UInt32> crcs) meadowFiles, (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
                return;

            for(int i = 0; i < localFiles.files.Count; i++)
            {
                if (meadowFiles.crcs.Contains(localFiles.crcs[i])) continue;

                await WriteFileToMeadow(target, cts,
                    folder, localFiles.files[i], true);
            }
        }

        async Task WriteFileToMeadow(MeadowDeviceExecutionTarget target , CancellationTokenSource cts, string folder, string file, bool overwrite = false)
        {
            if (cts.IsCancellationRequested) { return; }

            if (overwrite || await target.meadowSerialDevice.IsFileOnDevice(file).ConfigureAwait(false) == false)
            {
                target.Write($"Writing {file}\n");
                await target.meadowSerialDevice.WriteFile(file, folder).ConfigureAwait(false);
            }
        }

        async Task ResetMeadowAndStartMono(MeadowDeviceExecutionTarget target , CancellationTokenSource cts)
        {
            if(cts.IsCancellationRequested) { return; }

            string serial = target.meadowSerialDevice.DeviceInfo.SerialNumber;

            target.WriteSuccess("Resetting Meadow and starting app\n");

            MeadowDeviceManager.MonoEnable(target.meadowSerialDevice);

            MeadowDeviceManager.ResetMeadow(target.meadowSerialDevice, 0);

           
        }
    }
}