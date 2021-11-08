using System;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using MonoDevelop.Core.Execution;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowSoftDebuggerSession : SoftDebuggerSession
    {
        public MeadowSoftDebuggerSession()
        {
            debugCancelTokenSource = new CancellationTokenSource();    
        }

        CancellationTokenSource debugCancelTokenSource;
        MeadowSoftDebuggerStartInfo meadowStartInfo;

        protected override async void OnRun(DebuggerStartInfo startInfo)
        {
            try
            {
                meadowStartInfo = startInfo as MeadowSoftDebuggerStartInfo;

                var connectArgs = meadowStartInfo.StartArgs as SoftDebuggerConnectArgs;
                var port = connectArgs?.DebugPort ?? 0;

                await meadowStartInfo.ExecutionCommand.DeployApp(port, debugCancelTokenSource.Token);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Deploy error: " + ex.Message);
            }

            base.OnRun(startInfo);
        }

        protected override void OnExit()
        {
            CleanUp();

            try
            {
                base.OnExit();
            } catch { }
        }

        void CleanUp()
        {
            if (!debugCancelTokenSource.IsCancellationRequested)
                debugCancelTokenSource?.Cancel();

            meadowStartInfo?.ExecutionCommand?.Cleanup();
        }
    }
}