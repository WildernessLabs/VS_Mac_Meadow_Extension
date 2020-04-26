using System;
using System.Text;
using System.Threading;
using Gtk;
using Meadow.CLI.DeviceManagement;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;
using MeadowCLI.Hcom;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Represents a Meadow Device execution target; which is the actual
    /// device that gets deployed to when executing.
    /// </summary>
    public class MeadowDeviceExecutionTarget : ExecutionTarget
    {

        static uint PadID = 0;
        public override string Id => meadowSerialDevice?.DeviceInfo?.SerialNumber;
        public override string Name => "Meadow " + meadowSerialDevice?.DeviceInfo?.SerialNumber;

        public EventHandler<string> SerialNumberSet;

        public MeadowSerialDevice meadowSerialDevice { get; private set; }
        
        public MeadowPad meadowPad { get; private set; }

        public MeadowDeviceExecutionTarget(MeadowSerialDevice meadowSerialDevice, Connection connection = null)
        {
            this.meadowSerialDevice = meadowSerialDevice;
            var waitHandle = new ManualResetEventSlim(false);
            Gtk.Application.Invoke (delegate
            {   try
                {
                    meadowPad = new MeadowPad(this,meadowSerialDevice);
                    var pad = MonoDevelop.Ide.IdeApp.Workbench.ShowPad(meadowPad, (PadID++).ToString(), "Meadow", "bottom", MonoDevelop.Components.Docking.DockItemStatus.Dockable, new IconId("meadow"));
                    pad.Sticky = true;
                    pad.AutoHide = false;
                    pad.BringToFront();
                    pad.Visible = true;
                    pad.IsOpenedAutomatically = true;
                    
                    waitHandle.Set();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MeadowDeviceExecutionTarget: Error {ex.Message}");
                }
            });
            if (waitHandle.Wait(3000))
            {
                meadowSerialDevice.StatusChange += StatusDisplay;
                meadowSerialDevice.RunStateChange += RunState;
                if (meadowSerialDevice.RunState.HasValue) meadowPad.control.SetRunState(meadowSerialDevice.RunState.Value);
                meadowSerialDevice.connection = connection;
                Gtk.Application.Invoke(delegate
                {
                    meadowPad.Window.Title = meadowSerialDevice.connection.ToString();
                });
            }
            else
            {
                MessageDialog dialog = new MessageDialog(IdeApp.Workbench.RootWindow, DialogFlags.DestroyWithParent,
                                      MessageType.Info, ButtonsType.Ok,
                                      "Failed to open Meadow console window");
            }
        }

        string _serialNumber;
        public string SerialNumber
        {
            get
            {
                return _serialNumber;
            }
            private set
            {
                _serialNumber = value;
                SerialNumberSet?.Invoke(this,value);
            }
        }

        public void RunState (object sender, bool? runState)
        {
            meadowPad.control.SetRunState(runState.Value);
        }


        public void StatusDisplay (object sender, MeadowSerialDevice.DeviceStatus status)
        {
            if (status == MeadowSerialDevice.DeviceStatus.PortOpenGotInfo) SerialNumber = meadowSerialDevice.DeviceInfo.SerialNumber;
            meadowPad.control.SetStatus(status);     
        }

        public void Write(string text)
        {
            meadowPad.control.WriteToConsole(text);
        }

        public void WriteError(string text)
        {
            meadowPad.control.WriteToConsole(text, meadowPad.control.tagRedBold);
        }
        

        internal void WriteSuccess(string text)
        {
            meadowPad.control.WriteToConsole(text, meadowPad.control.tagGreen);
        }

        override public string ToString()
        {
            return $"{Name}";
        }
        
    }
}