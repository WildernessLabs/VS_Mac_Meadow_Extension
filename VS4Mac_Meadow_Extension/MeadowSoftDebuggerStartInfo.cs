using Mono.Debugging.Soft;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowSoftDebuggerStartInfo : SoftDebuggerStartInfo
    {
        public MeadowSoftDebuggerStartInfo(MeadowExecutionCommand cmd, SoftDebuggerStartArgs args)
            : base(args)
        {
            ExecutionCommand = cmd;
        }

        public readonly MeadowExecutionCommand ExecutionCommand;
    }
}