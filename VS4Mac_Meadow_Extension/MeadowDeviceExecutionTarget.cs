﻿using MeadowCLI.DeviceManagement;
using MonoDevelop.Core.Execution;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Represents a Meadow Device execution target; which is the actual
    /// device that gets deployed to when executing.
    /// </summary>
    public class MeadowDeviceExecutionTarget : ExecutionTarget
    {
        public override string Id
        {
            get
            {
                if(MeadowDevice?.DeviceInfo?.SerialNumber != null)
                {
                    return MeadowDevice?.DeviceInfo?.SerialNumber;
                }
                else
                {
                    return MeadowDevice?.PortName;
                }
            }
        }

        public override string Name => "Meadow " + MeadowDevice?.PortName;

        //public override string Name => "Meadow " + MeadowDevice?.DeviceInfo.SerialNumber.Substring("Serial Number: ".Length);

        public MeadowSerialDevice MeadowDevice { get; private set; }

        public MeadowDeviceExecutionTarget(MeadowSerialDevice device)
        {
            MeadowDevice = device;
        }
    }
}