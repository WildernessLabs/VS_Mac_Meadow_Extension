using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using MonoDevelop.Core.Execution;
using MonoDevelop.Debugger;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowSoftDebuggerEngine : DebuggerEngineBackend
	{
		public override bool CanDebugCommand(ExecutionCommand cmd)
            => cmd is MeadowExecutionCommand;

        public override bool IsDefaultDebugger(ExecutionCommand cmd)
            => cmd is MeadowExecutionCommand;

        public override DebuggerSession CreateSession()
            => new MeadowSoftDebuggerSession();

        public override DebuggerStartInfo CreateDebuggerStartInfo(ExecutionCommand cmd)
		{
            if (cmd is MeadowExecutionCommand command)
            {
                var args = new SoftDebuggerConnectArgs(string.Empty, System.Net.IPAddress.Loopback, 55898)
                {
                    MaxConnectionAttempts = 20,
                    TimeBetweenConnectionAttempts = 500
                };

                var startInfo = new MeadowSoftDebuggerStartInfo(command, args);

                return startInfo;
            }

            return null;
		}
    }
}