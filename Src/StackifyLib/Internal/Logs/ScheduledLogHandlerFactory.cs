using StackifyLib.Internal.Scheduling;
using StackifyLib.Internal.StackifyApi;

namespace StackifyLib.Internal.Logs
{
    internal static class ScheduledLogHandlerFactory
    {
        internal static IScheduledLogHandler Get() 
        {
            var stackifyApiService = StackifyApiServiceFactory.Get();
            var scheduler = new Scheduler();
            var appLogQueues = new AppLogQueues(Logger.MaxLogBufferSize);
            return new ScheduledLogHandler(stackifyApiService, scheduler, appLogQueues);
        }
    }
}