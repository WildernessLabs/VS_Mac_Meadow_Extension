using System;
using System.Diagnostics;
using System.Threading;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

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

                var configuration = IdeApp.Workspace.ActiveConfiguration;

                bool includePdbs = configuration is SolutionConfigurationSelector isScs
                    && isScs?.Id == "Debug"
                    && port > 1000;

                await meadowStartInfo.ExecutionCommand.DeployApp(port, includePdbs, debugCancelTokenSource.Token);

                base.OnRun(startInfo);
            }
            catch(Exception ex)
            {
                Console.WriteLine ($"OnRun() Error: {ex.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}" );
                CleanUp();
            }
        }

        protected override void OnExit()
        {
            try
            {
                CleanUp();

                base.OnExit();
            }
            catch(Exception ex)
            {
                Debug.WriteLine ($"OnExit() Error: {ex.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}");
            }
        }

        void CleanUp()
        {
            if (!debugCancelTokenSource.IsCancellationRequested)
                debugCancelTokenSource?.Cancel();

            meadowStartInfo?.ExecutionCommand?.Cleanup();
        }
    }
}