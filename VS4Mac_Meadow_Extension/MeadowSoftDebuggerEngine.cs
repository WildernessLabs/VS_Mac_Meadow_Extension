using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using MonoDevelop.Core.Execution;
using MonoDevelop.Debugger;
using MonoDevelop.Debugger.Soft;

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

                // TODO: Get User Assemblies (not system references and call to set them)
                // SoftDebuggerEngine.SetUserAssemblyNames(startInfo, new List<string> { });
                return startInfo;
            }

            return null;
		}
    }
}