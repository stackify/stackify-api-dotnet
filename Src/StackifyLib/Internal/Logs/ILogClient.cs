using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    public interface ILogClient
    {
        ///<summary>
        /// Whether or not the queue is available
        ///</summary>
        bool CanQueue();

        /// <summary>
        /// Close and flush the log queue
        /// </summary>
        void Close();

        /// <summary>
        /// Add a message to the queue for the current app
        /// </summary>
        void QueueMessage(LogMsg msg);

        /// <summary>
        /// Add a message to the queue for a specific app
        /// </summary>
        void QueueMessage(LogMsg msg, AppClaims appClaims);

        bool ErrorShouldBeSent(StackifyError error);
        void PauseUpload(bool isPaused);
    }
}