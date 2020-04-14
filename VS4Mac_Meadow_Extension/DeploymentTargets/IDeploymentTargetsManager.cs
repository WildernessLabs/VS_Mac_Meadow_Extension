using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.DeviceManagement;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Manages the deployment targets in the toolbar and other places.
    /// </summary>
    public interface IDeploymentTargetsManager : IDisposable
    {
        event Action<object> DeviceListChanged;

        Task StartPollingForDevices();

        void StopPollingForDevices();

        Task PausePollingForDevices(int seconds);

        MeadowDeviceExecutionTarget[] GetTargetList();
    }
}