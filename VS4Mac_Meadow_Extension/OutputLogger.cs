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
            if (!IsEnabled(logLevel))
                return;

            var msg = formatter(state, exception);

            // This appears in the "Tools Output" tab
            toolMonitor?.Log.Write(msg + Environment.NewLine);
        }

        public void ReportFileProgress(string filename, int percentage)
        {
            if (statusMonitor is null)
                statusMonitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor("File Transfer", IconId.Null, false);

            if (percentage < 1)
            {
                statusMonitor?.BeginTask($"Transferring: {filename}", TOTAL_PROGRESS);
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

        public void ReportDeviceMessage(string deviceMessage)
        {
            // This appears in the "Meadow" tab
            monitor?.Log.WriteLine(deviceMessage);
        }
    }
}