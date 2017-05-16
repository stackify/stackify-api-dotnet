using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    public static class LogClientFactory
    {
        public static ILogClient GetClient(string loggerName) 
        {
            var queue = ScheduledLogHandlerFactory.Get();
            var errorGovernor = new ErrorGovernor();
            return new LogClient(queue, errorGovernor, loggerName);
        }
    }
}