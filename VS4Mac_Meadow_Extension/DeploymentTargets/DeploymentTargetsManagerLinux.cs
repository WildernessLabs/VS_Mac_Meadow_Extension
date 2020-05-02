using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meadow.CLI.DeviceManagement;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;
using MonoDevelop.Ide.Gui;
using System.Linq;
using MeadowCLI;

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
                var expiredTargets = Targets.FindAll(x => x.meadowSerialDevice.connection.Removed && x.meadowSerialDevice.connection.TimeRemoved.AddSeconds(SecondsToRemove) < DateTime.UtcNow);
                foreach (var target in expiredTargets)
                {
                    //Check if meadow is NOT in boot/dfu mode
                    if (!Meadow.CLI.Internals.Udev.Udev.GetSerialNumbers("0483", "df11", "usb").Contains(target.SerialNumber))
                    {
                        // Remove Pad from Monodevelop
                        Gtk.Application.Invoke(delegate
                        {
                            foreach (var pad in MonoDevelop.Ide.IdeApp.Workbench.Pads.FindAll(x => x?.Content is MeadowPad && ((MeadowPad)x.Content).Target == target))
                            {
                                target.meadowPad.Window.Visible = false;
                                MonoDevelop.Ide.IdeApp.Workbench.Pads.Remove(pad);
                            }
                        });
                        Targets.Remove(target);
                    }
                }
                if (expiredTargets.Count > 0) DeviceListChanged?.Invoke(null);                
            }
        }

        public MeadowDeviceExecutionTarget[] GetTargetList()
        {
            lock (_lockObject)
            {
                return Targets.ToArray();
            }
        }
        
        public uint Count
        {
            get
            {
                return (uint)Targets.Count;
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

            await Task<MeadowDeviceExecutionTarget>.Run(() =>
            {
               var meadowSerialDevice = new MeadowSerialDevice(true);
               if (MeadowDeviceManager.CurrentDevice == null || (MeadowDeviceManager.CurrentDevice?.connection.Removed ?? true)) MeadowDeviceManager.CurrentDevice = meadowSerialDevice;

                lock (_lockObject)
                {
                    target = new MeadowDeviceExecutionTarget(meadowSerialDevice,connection);
                    if (target != null)
                    {
                        Console.WriteLine($"DeploymentTargetsManagerLinux Added: {target}");
                        Targets.Add(target);
                        target.SerialNumberSet += Target_SerialNumberSet;
                        DeviceListChanged?.Invoke(target);
                    }
                }
            });
            return target;
        }

        void Target_SerialNumberSet(object sender, string e)
        {
        }


        public void Dispose()
        {

            foreach (var target in Targets)
            {
                target.Dispose();
            }
            ConnectionMonitor.DeviceNew -= DeviceNew;
            ConnectionMonitor.DeviceRemoved -= DeviceRemoved;
            ConnectionMonitor.Dispose();
        }
        
    }
}