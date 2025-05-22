using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Meadow.Hcom;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using Microsoft.Extensions.Logging;

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

        DebuggingServer meadowDebugServer = null;

        protected override async void OnRun(DebuggerStartInfo startInfo)
        {
            try
            {
                if (startInfo is MeadowSoftDebuggerStartInfo meadowSoftDebuggerStartInfo)
                {
                    meadowStartInfo = meadowSoftDebuggerStartInfo;

                    var connectArgs = meadowStartInfo.StartArgs as SoftDebuggerConnectArgs;
                    var port = connectArgs?.DebugPort ?? 0;

                    var configuration = IdeApp.Workspace.ActiveConfiguration;

                    bool includePdbs = configuration is SolutionConfigurationSelector isScs
                        && isScs?.Id == "Debug"
                        && port > 1000;

                    await meadowStartInfo.ExecutionCommand.DeployApp(port, includePdbs, debugCancelTokenSource.Token);

                    await Task.Run(()=> base.OnRun(meadowStartInfo));

                    if (includePdbs)
                    {
                        meadowStartInfo.ExecutionCommand.Logger.LogInformation("Debugging application...");
                        await Runtime.RunInMainThread(async () =>
                        {
                            meadowDebugServer = await meadowStartInfo.ExecutionCommand.MeadowConnection?.StartDebuggingSession(port, meadowStartInfo.ExecutionCommand.Logger, debugCancelTokenSource.Token, "VS 4 Mac");
                        });
                    }
                    else
                    {
                        // sleep until cancel since this is a normal deploy without debug
                        while (!debugCancelTokenSource.Token.IsCancellationRequested)
                            await Task.Delay(1000, debugCancelTokenSource.Token);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Parameter {nameof(startInfo)} type is invalid.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnRun() Error: {ex.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}");
                await CleanUp();
            }
        }

        protected override void OnExit()
        {
            try
            {
                _ = Task.Run(async () => { await this.CleanUp(); });

                base.OnExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnExit() Error: {ex.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}");
            }
        }

        async Task CleanUp()
        {
            if (!debugCancelTokenSource.IsCancellationRequested)
                debugCancelTokenSource?.Cancel();
        }

        public override void Dispose()
        {
            debugCancelTokenSource?.Dispose();

            base.Dispose();
        }
    }
}