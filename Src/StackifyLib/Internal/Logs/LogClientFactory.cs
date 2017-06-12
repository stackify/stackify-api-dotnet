using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    public static class LogClientFactory
    {
        public static ILogClient GetClient(string loggerName) 
        {
            var scheduledLogHandler = ScheduledLogHandlerFactory.Get();
            var errorGovernor = new ErrorGovernor();
            return new LogClient(scheduledLogHandler, errorGovernor, loggerName);
        }
    }
}