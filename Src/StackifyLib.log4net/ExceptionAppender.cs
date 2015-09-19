using log4net.Core;
using Apache_log4net = log4net;

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
