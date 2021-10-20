using MonoDevelop.Core.Execution;
using System.Threading.Tasks;
using System.Threading;
using MonoDevelop.Core;
using System.Collections.Generic;
using System;
using System.IO;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Devices;

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
                return new ProcessAsyncOperation(meadowCommand.DeployApp(-1, cts.Token), cts);
            }

            return null;
        }
    }
}