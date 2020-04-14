using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meadow.CLI.DeviceManagement;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Manages the deployment targets in the toolbar and other places.
    /// </summary>
    public  class DeploymentTargetsManagerLinux : IDeploymentTargetsManager
    {
        /// <summary>
        /// A collection of connected and ready Meadow devices
        /// </summary>
        private  List<MeadowDeviceExecutionTarget> Targets = new List<MeadowDeviceExecutionTarget>();
        readonly object _lockObject = new object();

        IConnectionMonitor ConnectionMonitor;
        const int SecondsToRemove = 6;

        public  event Action<object> DeviceListChanged;


        public DeploymentTargetsManagerLinux()
        {
            ConnectionMonitor = new ConnectionMonitorUdev(true);

            ConnectionMonitor.DeviceNew += DeviceNew;
            ConnectionMonitor.DeviceRemoved += DeviceRemoved;

            foreach (Connection connection in ConnectionMonitor.GetDeviceList())
            {
                InitalizeMeadow(connection);
            }            
        }


        void RemoveExpiredTargets()
        {
            
            lock (_lockObject)
            {
                if (Targets.RemoveAll(x => x.meadowSerialDevice.connection.Removed && x.meadowSerialDevice.connection.TimeRemoved.AddSeconds(SecondsToRemove) < DateTime.UtcNow) > 0)
                {
                    DeviceListChanged?.Invoke(null);
                }
            }
        }

        public MeadowDeviceExecutionTarget[] GetTargetList()
        {
            lock (_lockObject)
            {
                return Targets.ToArray();
            }
        }

        private void DeviceNew(object sender, Connection connection)
        {
        
            //First we need to check if we already have a target for this device.
            var target = Targets.Find(x => x.meadowSerialDevice.connection.IsMatch(connection));

            //If there is no target, we need to Add it
            if (target == null)
            {
                InitalizeMeadow(connection);
            }
            else
            {
                target.meadowSerialDevice.connection = connection;                  // Update the connection
            }
            RemoveExpiredTargets();
        }

        private void DeviceRemoved(object sender, Connection connection)
        {            
            //Dont need to do anything here - Connection removed event should have taken care of it.
            RemoveExpiredTargets();
        }


        async Task<MeadowDeviceExecutionTarget> InitalizeMeadow(Connection connection)
        {
            MeadowDeviceExecutionTarget target = null;
               
            var meadowTask = MeadowDeviceManager.GetMeadowForConnection(connection);

            if (await Task.WhenAny(meadowTask, Task.Delay(5000)) == meadowTask)
            {
                var meadowSerialDevice = meadowTask.Result;

                if (meadowSerialDevice != null)
                {
                    target = new MeadowDeviceExecutionTarget(meadowSerialDevice);
                    if (target != null)
                    {
                        Console.WriteLine($"DeploymentTargetsManagerLinux Added: {target}");
                        lock (_lockObject)
                        {
                            Targets.Add(target);
                        }
                        DeviceListChanged?.Invoke(target);
                    }
                }
            }

            return target;
        }

        

        public async Task StartPollingForDevices()
        {
            //UsbDeviceManager takes care of it
        }

        public  void StopPollingForDevices()
        {
            //UsbDeviceManager takes care of it
        }

        public  async Task PausePollingForDevices(int seconds = 15)
        {
           //UsbDeviceManager takes care of it
        }

        public void Dispose()
        {
        
            ConnectionMonitor.DeviceNew -= DeviceNew;
            ConnectionMonitor.DeviceRemoved -= DeviceRemoved;        
        }
        
    }
}