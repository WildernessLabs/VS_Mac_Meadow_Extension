using System;
using System.IO;
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

        static string SerialPortName = "/dev/tty.usbmodem3371335C30361";
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

            Thread.Sleep(-1);
        }

        static IMeadowDevice meadow;

        public static async Task Deploy()
        {
            var cts = new CancellationTokenSource();

            logger = new OutputLogger();

            try
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, logger: logger).ConfigureAwait(false);

                var meadowDH = new MeadowDeviceHelper(meadow, logger);

                await meadowDH.MonoDisableAsync(cts.Token);

                await meadowDH.DeployAppAsync("//Users//adrianstevens//Projects//Blink51Standard//Blink51Standard//bin//Debug//netstandard2.1//App.exe",
                    true, cts.Token);

                await meadowDH.MonoEnableAsync(cts.Token);

                meadow = meadowDH.MeadowDevice; //reference to keep alive
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }
}
