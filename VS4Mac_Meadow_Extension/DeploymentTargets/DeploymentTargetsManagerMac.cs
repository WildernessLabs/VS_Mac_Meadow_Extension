using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Manages the deployment targets in the toolbar and other places.
    /// </summary>
    public class DeploymentTargetsManagerMac : IDeploymentTargetsManager
    {
        /// <summary>
        /// A collection of connected and ready Meadow devices
        /// </summary>
        public List<MeadowDeviceExecutionTarget> Targets { get; } = new List<MeadowDeviceExecutionTarget>();

        //    private static Timer devicePollTimer;
        //    private static object eventLock = new object();
        private bool isPolling;
        private CancellationTokenSource cts;

        public event Action<object> DeviceListChanged;

        public async Task StartPollingForDevices()
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

                if(cts.IsCancellationRequested)
                {
                    isPolling = false;
                }
            }

            isPolling = false;
        }

        public void StopPollingForDevices()
        {
            cts.Cancel();
        }

        public async Task PausePollingForDevices(int seconds = 15)
        {
            StopPollingForDevices();
            await Task.Delay(seconds * 1000);
            StartPollingForDevices();
        }

        private async Task UpdateTargetsList(CancellationToken ct)
        {
            var serialPorts = MeadowDeviceManager.FindSerialDevices();

            foreach(var port in serialPorts)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (Targets.Any(t => t.meadowSerialDevice.connection?.USB?.DevicePort == port))
                    continue;

                var timeout = Task<MeadowDevice>.Delay(1000);
                var meadowTask = MeadowDeviceManager.GetMeadowForSerialPort(port);

                await Task.WhenAny(timeout, meadowTask);

                var meadow = meadowTask.Result;

                if (meadow != null)
                {
                    //we should really just have the execution target own an instance of MeadowDevice 
                    Targets.Add(new MeadowDeviceExecutionTarget(meadow));
                    //meadow.CloseConnection();
                    DeviceListChanged?.Invoke(null);
                    meadow.StatusChange += StatusDisplay;
                }
            }

            var removeList = new List<MeadowDeviceExecutionTarget>();
            foreach(var t in Targets)
            {
                if(serialPorts.Any(p => p == t.meadowSerialDevice.connection?.USB?.DevicePort) == false)
                {
                    removeList.Add(t);
                }
            }

            foreach(var r in removeList)
            {
                r.meadowSerialDevice.StatusChange -= StatusDisplay;
                Targets.Remove(r);
                DeviceListChanged?.Invoke(null);
            }
        }

        public void StatusDisplay(object sender, MeadowSerialDevice.DeviceStatus status)
        {
            var meadow = (MeadowSerialDevice)sender;

            switch (status)
            {
                case MeadowSerialDevice.DeviceStatus.Disconnected:

                    Task.Run(() =>
                    {
                        Thread.Sleep(3000);

                        var connection = new Connection()
                        {
                            Mode = MeadowMode.MeadowMono,
                            USB = new Connection.USB_interface()
                            {                
                                 DevicePort = meadow.connection.USB.DevicePort,
                            }
                        };
                        meadow.connection = connection;
                    });
                    break;
            }
        }
        
        public MeadowDeviceExecutionTarget[] GetTargetList()
        {
                return Targets.ToArray();
        }

        public void Dispose()
        {
        }
    }
}