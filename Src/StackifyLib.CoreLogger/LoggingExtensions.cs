using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StackifyLib.CoreLogger
{
    public static class LoggingExtensions
    {
        public static ILoggerFactory AddStackify(this ILoggerFactory factory, ILogger logger = null, bool dispose = false)
        {
            if (factory == null)
                throw new ArgumentNullException("factory");

            factory.AddProvider((ILoggerProvider)new StackifyLoggerProvider());
            return factory;
        }
    }
}
