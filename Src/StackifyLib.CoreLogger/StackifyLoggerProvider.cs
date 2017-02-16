using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StackifyLib.CoreLogger
{
    public class StackifyLoggerProvider : ILoggerProvider, IDisposable
    {
        public StackifyLoggerProvider()
        {
        }


        public Microsoft.Extensions.Logging.ILogger CreateLogger(string name)
        {
            return (Microsoft.Extensions.Logging.ILogger)new StackifyLogger();
        }

        public void Dispose()
        {
        }
    }
}
