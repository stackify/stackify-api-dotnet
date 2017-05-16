using System.Threading.Tasks;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    internal interface IScheduledLogHandler
    {
        bool CanQueue();
        Task Stop();
        void Pause(bool isPaused);
        void QueueLogMessage(AppClaims app, LogMsg msg);
    }
}