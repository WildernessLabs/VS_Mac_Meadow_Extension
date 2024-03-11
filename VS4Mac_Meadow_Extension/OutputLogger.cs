using System;
using Microsoft.Extensions.Logging;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class OutputLogger : ILogger
    {
        static ProgressMonitor monitor;
        static ProgressMonitor statusMonitor;
        static OutputProgressMonitor toolMonitor;
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
            /*if (!IsEnabled(logLevel))
                return;*/

            var msg = formatter(state, exception);

            switch (logLevel)
            {
                case LogLevel.Trace:
                    break;
                case LogLevel.Debug:
                    // This appears in the "Meadow" tab
                    monitor?.Log.WriteLine(msg);
                    break;
                case LogLevel.Information:
                    // This appears in the "Tools Output" tab
                    toolMonitor?.Log.Write(msg + Environment.NewLine);
                    break;
                case LogLevel.Warning:
                    break;
                case LogLevel.Error:
                    break;
                case LogLevel.Critical:
                    break;
                case LogLevel.None:
                    break;
                default:
                    break;
            }
            /*if (msg.Contains("StdOut") || msg.Contains("StdInfo"))
            {
                // This appears in the "Meadow" tab
                monitor?.Log.WriteLine(msg.Substring(15));
            }
            else
            {
                // This appears in the "Tools Output" tab
                toolMonitor?.Log.Write(msg + Environment.NewLine);
            }*/
        }

        public void Report(string filename, int percentage)
        {
            if (statusMonitor is null)
                statusMonitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("File Transferring", IconId.Null, false);

            if (percentage < 1)
            {
                statusMonitor?.BeginTask($"File Transferring: {filename}", TOTAL_PROGRESS);
            }

            if (percentage >= 1 && percentage <= 99)
            {
                statusMonitor?.Step(percentage);
            }

            if (percentage > 99)
            {
                statusMonitor?.EndTask();
            }
        }
    }
}