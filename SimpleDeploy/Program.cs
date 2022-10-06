using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Internals.MeadowCommunication;

namespace SimpleDeploy
{
    class MainClass
    {
        static string AppPath = "//Users//adrianstevens//Projects//Blink51Standard//Blink51Standard//bin//Debug//netstandard2.1";

        static string SerialPortName = "/dev/tty.usbmodem3471387235361";
        //"//dev//tty.usbmodem3471387235361";

        static OutputLogger logger;

        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            
            try
            {
                Deploy().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }

            Thread.Sleep(5000);
        }

        static IMeadowDevice meadow;

        public static async Task Deploy()
        {
            var cts = new CancellationTokenSource();

            logger = new OutputLogger();

            try
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, logger: logger);

                var meadowDH = new MeadowDeviceHelper(meadow, logger);

                await meadowDH.MonoDisableAsync(false, cts.Token);

                //meadowDH.DeployAppAsync();

                var items = await meadowDH.GetFilesAndFoldersAsync(new TimeSpan(0, 0, 10), cts.Token);

                bool isRoot = false;
                bool isFolder = false;
                string folderName = string.Empty;

                var filesToDelete = new List<string>();

                for (int i = 0; i < items.Count; i++)
                {   //start of folders in root
                    if (isRoot == false && items[i].Contains("meadow0/"))
                    {
                        isRoot = true;
                    } //next root folder - break out
                    else if (isRoot && items[i].Contains(" ") == false)
                    {
                        break;
                    } //item under meadow0
                    else if (isRoot &&
                        items[i].Contains("/") &&
                        items[i].Contains("./") == false)
                    {
                        folderName = items[i].Substring(1);
                        isFolder = true;
                    }
                    else if (isFolder == true &&
                        items[i].Contains("  ") &&
                        items[i].Contains("[file]"))
                    {
                        var end = items[i].IndexOf(" [file]");
                        filesToDelete.Add(Path.Combine(folderName, items[i].Substring(2, end - 2)));
                    }
                    else
                    {   
                        continue;
                    }
                }

                foreach (var item in filesToDelete)
                {
                    if(item.Contains("Data/") ||
                        item.Contains("Documents/"))
                    {
                        continue;
                    }

                    Console.WriteLine($"Deleting {item}");
                    await meadow.DeleteFileAsync(item, 0, cts.Token);
                }

                

                /*
                await meadowDH.DeployAppAsync("//Users//adrianstevens//Projects//Blink51Standard//Blink51Standard//bin//Debug//netstandard2.1//App.exe",
                    true, cts.Token);

                await meadowDH.MonoEnableAsync(cts.Token);

                meadow = meadowDH.MeadowDevice; //reference to keep alive
                */
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }
}
