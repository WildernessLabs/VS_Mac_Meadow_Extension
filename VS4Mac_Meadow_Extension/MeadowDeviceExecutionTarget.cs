using MonoDevelop.Core.Execution;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    /// <summary>
    /// Represents a Meadow Device execution target; which is the actual
    /// device that gets deployed to when executing.
    /// </summary>
    public class MeadowDeviceExecutionTarget : ExecutionTarget
    {
        public override string Id => Port;

        public override string Name => $"Meadow {Port}";

        public string Port { get; private set; }

        public MeadowDeviceExecutionTarget(string port)
        {
            Port = port;
        }
    }
}