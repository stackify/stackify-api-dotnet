using System;
using Microsoft.Extensions.Logging;
using StackifyLib.Models;

namespace StackifyLib.AspNetCore
{
    public class StackifyLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (formatter == null)
                throw new ArgumentNullException("formatter");
            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
                return;

            var msg = new LogMsg()
            {
                Level = logLevel.ToString(),
                Msg = message
            };

            StackifyLib.Logger.QueueLogObject(msg, exception);
        }

        public void Close()
        {
            try
            {
                StackifyLib.Logger.Shutdown();
            }
            catch (Exception)
            {
                
            }
        }
    }
}
