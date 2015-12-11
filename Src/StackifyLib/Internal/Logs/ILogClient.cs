using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    public interface ILogClient
    {
        bool CanQueue();
        bool CanSend();
        bool CanUpload();
        void Close();
        bool ErrorShouldBeSent(StackifyError error);
        AppIdentityInfo GetIdentity();
        bool IsAuthorized();
        void PauseUpload(bool isPaused);
        void QueueMessage(LogMsg msg);
    }
}