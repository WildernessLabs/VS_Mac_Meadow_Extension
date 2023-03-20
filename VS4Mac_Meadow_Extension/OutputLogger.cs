using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Pads;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class OutputLogger : ILogger
    {
        static ProgressMonitor monitor;
        static ProgressMonitor statusMonitor;
        static OutputProgressMonitor toolMonitor;
        int nextProgress = 0;
        const int PROGRESS_INCREMENT = 5;
        const int TOTAL_PROGRESS = 100;

        public OutputLogger()
        {
            if (monitor is null)
            {
                toolMonitor = IdeApp.Workbench.ProgressMonitors.GetToolOutputProgressMonitor(true);
            }
            else
            {
                monitor.Dispose();
                monitor = null;
            }

            // Create/Recreate it
            monitor = IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor("Meadow", IconId.Null, true, true, true);
        }

        public IDisposable BeginScope<TState>(TState state) => default;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var msg = formatter(state, exception);

            if (msg.Contains("StdOut") || msg.Contains("StdInfo"))
            {
                // This appears in the "Meadow" tab
                monitor?.Log.WriteLine(msg.Substring(15));
            }
            else if(msg == "[")
            {
                if (statusMonitor is null)
                    statusMonitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("File Transferring", IconId.Null, false);

                nextProgress = 0;
                statusMonitor.BeginTask("File Transferrring", TOTAL_PROGRESS);

            }
            else if (msg == "]")
            {
                statusMonitor.EndTask();
            }
            else if (msg == "=")
            {
                statusMonitor.Step(nextProgress);
                nextProgress += PROGRESS_INCREMENT;
            }
            else
            {
                // This appears in the "Tools Output" tab
                toolMonitor?.Log.Write(msg);
            }
        }
    }
}