using Meadow.CLI.DeviceManagement;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;
using MonoDevelop.Core.Execution;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Represents a Meadow Device execution target; which is the actual
    /// device that gets deployed to when executing.
    /// </summary>
    public class MeadowDeviceExecutionTarget : ExecutionTarget
    {
        public override string Id => meadowSerialDevice?.DeviceInfo?.SerialNumber;

        public override string Name => "Meadow " + meadowSerialDevice?.DeviceInfo?.SerialNumber?.Substring("Serial Number: ".Length);

        public MeadowSerialDevice meadowSerialDevice { get; set; }

        public MeadowDeviceExecutionTarget(MeadowSerialDevice meadowSerialDevice, Connection connection = null)
        {
            this.meadowSerialDevice = meadowSerialDevice;
        }

        override public string ToString()
        {
            return $"{Name}";
        }
        
    }
}