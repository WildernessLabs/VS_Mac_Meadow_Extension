using System;
using Microsoft.Extensions.Logging;

namespace SimpleDeploy
{
    public class OutputLogger : ILogger
    {
        public OutputLogger()
        {
           
        }

        public IDisposable BeginScope<TState>(TState state) => default;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            Console.WriteLine($"{formatter(state, exception)}");
        }
    }
}