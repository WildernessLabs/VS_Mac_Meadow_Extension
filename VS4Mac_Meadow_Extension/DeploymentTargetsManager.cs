using System;
using System.Collections.Generic;
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

        //    private static Timer devicePollTimer;
        //    private static object eventLock = new object();
        private static bool isPolling;
        private static CancellationTokenSource cts;

        public static event Action<object> DeviceListChanged;

        public static async Task StartPollingForDevices()
        {
            if(isPolling == true)
            {
                return;
            }

            isPolling = true;

            cts = new CancellationTokenSource();

            while (isPolling)
            {
                await UpdateTargetsList(cts.Token);
                await Task.Delay(2000);
            }

            isPolling = false;
        }

        public static void StopPollingForDevices()
        {
            cts.Cancel();
        }

        private static async Task UpdateTargetsList(CancellationToken ct)
        {
            var serialPorts = MeadowDeviceManager.FindSerialDevices();

            foreach(var port in serialPorts)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (Targets.Any(t => t.MeadowDevice.SerialPort.PortName == port))
                    continue;

                var timeout = Task<MeadowDevice>.Delay(1000);
                var meadowTask = MeadowDeviceManager.GetMeadowForSerialPort(port);

                await Task.WhenAny(timeout, meadowTask);

                var meadow = meadowTask.Result;

                if (meadow != null)
                {
                    //we should really just have the execution target own an instance of MeadowDevice 
                    Targets.Add(new MeadowDeviceExecutionTarget(meadow));
                    meadow.SerialPort.Close();
                    DeviceListChanged?.Invoke(null);
                }
            }

            var removeList = new List<MeadowDeviceExecutionTarget>();
            foreach(var t in Targets)
            {
                if(serialPorts.Any(p => p == t.MeadowDevice.SerialPort.PortName) == false)
                {
                    removeList.Add(t);
                }
            }

            foreach(var r in removeList)
            {
                Targets.Remove(r);
                DeviceListChanged?.Invoke(null);
            }
        }
    }
}