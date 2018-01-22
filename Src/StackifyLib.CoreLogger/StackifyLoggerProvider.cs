using System;
using Microsoft.Extensions.Logging;

namespace StackifyLib.CoreLogger
{
    public class StackifyLoggerProvider : ILoggerProvider
    {
        private StackifyLogger _stackifyLogger;

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string name)
        {
            if (_stackifyLogger == null)
            {
                _stackifyLogger = new StackifyLogger();
            }

            return _stackifyLogger;
        }

        public void Dispose()
        {
            _stackifyLogger?.Close();
        }
    }
}