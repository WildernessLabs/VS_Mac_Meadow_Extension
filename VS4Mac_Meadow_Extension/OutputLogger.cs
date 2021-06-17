using System;
using Microsoft.Extensions.Logging;
using MonoDevelop.Core;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class OutputLogger : ILogger
    {
        static ProgressMonitor monitor;

        public OutputLogger()
        {
            if (monitor == null)
            {
               // monitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetToolOutputProgressMonitor(true);
                monitor = MonoDevelop.Ide.IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor("Meadow", IconId.Null, true, true, true);
            }
        }

        public IDisposable BeginScope<TState>(TState state) => default;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            monitor?.Log.WriteLine(formatter(state, exception));
        }
    }
}