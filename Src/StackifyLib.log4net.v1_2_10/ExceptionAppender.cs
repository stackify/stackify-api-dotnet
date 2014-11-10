using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net.Appender;
using System.Diagnostics;
using log4net.Core;
using Apache_log4net = log4net;
using StackifyLib.Utils;

namespace StackifyLib.log4net
{
    public class ExceptionAppender : StackifyAppender
    {
        protected override void Append(LoggingEvent loggingEvent)
        {
            if (Level.Error == loggingEvent.Level || Level.Fatal == loggingEvent.Level)
            {
                base.Append(loggingEvent);
            }
        }
    }
}
