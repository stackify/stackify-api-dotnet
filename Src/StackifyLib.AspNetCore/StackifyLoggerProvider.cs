using System;
using Microsoft.Extensions.Logging;

namespace StackifyLib.AspNetCore
{
    public class StackifyLoggerProvider : ILoggerProvider, IDisposable
    {
        private StackifyLogger _StackifyLogger = null;

        public ILogger CreateLogger(string name)
        {
            if (_StackifyLogger == null)
            {
                _StackifyLogger = new StackifyLogger();
            }

            return _StackifyLogger;
        }

        public void Dispose()
        {
            _StackifyLogger?.Close();
        }
    }
}
