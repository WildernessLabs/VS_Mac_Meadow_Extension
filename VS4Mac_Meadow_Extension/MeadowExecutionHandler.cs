using MonoDevelop.Core.Execution;
using System.Threading;
using System;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    class MeadowExecutionHandler : IExecutionHandler
    {
        public bool CanExecute(ExecutionCommand command)
            => command is MeadowExecutionCommand;

        public ProcessAsyncOperation Execute(ExecutionCommand command, OperationConsole console)
        {
            var cts = new CancellationTokenSource();

            if (command is MeadowExecutionCommand meadowCommand)
            {
                try
                {
                    return new ProcessAsyncOperation(meadowCommand.DeployApp(-1, false, cts.Token), cts);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return null;
        }
    }
}