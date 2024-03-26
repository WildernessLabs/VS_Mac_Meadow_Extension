using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Commands.DeviceManagement;

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
        public static List<MeadowDeviceExecutionTarget> Targets { get; private set; } = new List<MeadowDeviceExecutionTarget>();

        private static bool isPolling;
        private static CancellationTokenSource cancellationTokenSource = null;

        public static event Action<object> DeviceListChanged;

        public static async Task StartPollingForDevices()
        {
            Debug.WriteLine("Start Polling for devices");

            if(isPolling == true) { return; }

            isPolling = true;

            if (cancellationTokenSource == null)
            {
                cancellationTokenSource = new CancellationTokenSource();
            }

            while (cancellationTokenSource.IsCancellationRequested == false)
            {
                await Task.Run(async ()=> UpdateTargetsList(await MeadowConnectionManager.GetSerialPorts(), cancellationTokenSource.Token));
                await Task.Delay(5000);
            }

            isPolling = false;
        }

        public static void StopPollingForDevices()
        {
            cancellationTokenSource?.Cancel();
        }

        private static void UpdateTargetsList(IList<string> serialPorts, CancellationToken cancellationToken)
        {
            Debug.WriteLine("Update targets list");
            //var serialPorts = MeadowDeviceManager.FindSerialDevices();
            //use the local hack version that leverages ioreg

            foreach (var port in serialPorts)
            {
                if (cancellationToken.IsCancellationRequested)
                { break; }

                if (Targets.Any(t => t.Id == port))
                { continue; }

                Targets?.Add(new MeadowDeviceExecutionTarget(port));
                DeviceListChanged?.Invoke(null);
            }

            var removeList = new List<MeadowDeviceExecutionTarget>();
            foreach (var t in Targets)
            {
                if (serialPorts.Any(p => p == t.Id) == false)
                {
                    removeList.Add(t);
                }
            }

            foreach (var r in removeList)
            {
                Targets?.Remove(r);
                DeviceListChanged?.Invoke(null);
            }
        }
    }
}