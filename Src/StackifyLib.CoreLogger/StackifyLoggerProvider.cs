using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StackifyLib.CoreLogger
{
    public class StackifyLoggerProvider : ILoggerProvider, IDisposable
    {
        private StackifyLogger _StackifyLogger = null;

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string name)
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
