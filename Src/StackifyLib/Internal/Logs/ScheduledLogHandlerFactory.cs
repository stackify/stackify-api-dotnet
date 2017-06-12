using System;
using System.Diagnostics;
using StackifyLib.Internal.Scheduling;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    internal static class ScheduledLogHandlerFactory
    {
        internal static IScheduledLogHandler Get() => lazyLogHandler.Value;

        private static readonly Lazy<ScheduledLogHandler> lazyLogHandler =
            new Lazy<ScheduledLogHandler>(GetHandler, true);

        private static ScheduledLogHandler GetHandler()
        {
            var stackifyApiService = StackifyApiServiceFactory.Get();
            var scheduler = new Scheduler();
            var appLogQueues = new AppLogQueues(Config.MaxLogBufferSize);
            return new ScheduledLogHandler(stackifyApiService, scheduler, appLogQueues);
        }
    }
}