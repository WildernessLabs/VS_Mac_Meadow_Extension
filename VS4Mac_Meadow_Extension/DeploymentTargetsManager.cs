using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Manages the deployment targets in the toolbar and other places.
    /// </summary>
    public static class DeploymentTargetsManager
    {
        /// <summary>
        /// A collection of connected and ready Meadow devices
        /// </summary>
        public static List<MeadowDeviceExecutionTarget> Targets { get; } = new List<MeadowDeviceExecutionTarget>();

        private static bool isPolling;
        private static CancellationTokenSource cts;

        public static event Action<object> DeviceListChanged;

        public static async Task StartPollingForDevices()
        {
            Debug.WriteLine("Start Polling for devices");

            if(isPolling == true) { return; }

            isPolling = true;

            cts = new CancellationTokenSource();

            while (cts.IsCancellationRequested == false)
            {
                await Task.Run(()=> UpdateTargetsList(GetMeadowSerialPorts(), cts.Token));
                await Task.Delay(3000);
            }

            isPolling = false;
        }

        public static void StopPollingForDevices()
        {
            cts.Cancel();
        }

        //Experiemental use of ioreg to find serial ports for connected Meadow devices
        //Relies on string parsing - may break if macOS moves ioreg or the output of ioreg changes signifigantly
        //Adrian - my guess is, this is fairly stable 
        static List<string> GetMeadowSerialPorts()
        {
            Debug.WriteLine("Get Meadow Serial ports");
            var ports = new List<string>();

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/sbin/ioreg",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Arguments = "-r -c IOUSBHostDevice -l"
            };

            string output = string.Empty;

            using (var p = Process.Start(psi))
            {
                if (p != null)
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                }
            }

            //split into lines
            var lines = output.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            bool foundMeadow = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Meadow F7 Micro"))
                {
                    foundMeadow = true;
                }
                else if (lines[i].IndexOf("+-o") == 0)
                {
                    foundMeadow = false;
                }

                //now find the IODialinDevice entry which contains the serial port name
                if (foundMeadow && lines[i].Contains("IODialinDevice"))
                {
                    int startIndex = lines[i].IndexOf("/");
                    int endIndex = lines[i].IndexOf("\"", startIndex + 1);
                    var port = lines[i].Substring(startIndex, endIndex - startIndex);
                    Debug.WriteLine($"Found Meadow at {port}");

                    ports.Add(port);
                    foundMeadow = false;
                }
            }
            return ports;
        }

        private static void UpdateTargetsList(List<string> serialPorts, CancellationToken ct)
        {
            Debug.WriteLine("Update targets list");
            //var serialPorts = MeadowDeviceManager.FindSerialDevices();
            //use the local hack version that leverages ioreg

            foreach (var port in serialPorts)
            {
                if (ct.IsCancellationRequested)
                { break; }

                if (Targets.Any(t => t.MeadowDevice?.PortName == port))
                { continue; }

                var meadow = new MeadowSerialDevice(port, false);

                Targets?.Add(new MeadowDeviceExecutionTarget(meadow));
                DeviceListChanged?.Invoke(null);
            }

            var removeList = new List<MeadowDeviceExecutionTarget>();
            foreach (var t in Targets)
            {
                if (serialPorts.Any(p => p == t?.MeadowDevice?.PortName) == false)
                {
                    removeList.Add(t);
                }
            }

            foreach (var r in removeList)
            {
                r?.MeadowDevice?.SerialPort?.Close();
                Targets?.Remove(r);
                DeviceListChanged?.Invoke(null);
            }
        }
    }
}