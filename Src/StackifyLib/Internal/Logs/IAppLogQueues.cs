using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using StackifyLib.Models;
using StackifyLib.Utils;
using System.Threading;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.Scheduling;


namespace StackifyLib.Internal.Logs
{
    ///<summary>
    /// Manages log queues for each publishing application
    ///</summary>
    internal interface IAppLogQueues
    {
        ///<summary>
        /// Whether or not the sum across all app queues is greater than the configure maximum size
        ///</summary>
        bool IsFull { get; }

        ///<summary>
        /// True if there are no app queues or there are no messages in any queues
        ///</summary>
        bool IsEmpty { get; }

        ///<summary>
        /// Adds a message to an app's log queue
        ///</summary>
        void QueueMessage(AppClaims app, LogMsg msg);

        ///<summary>
        /// Gets the underlying queue for a specified app
        ///</summary>
        ConcurrentQueue<LogMsg> GetAppQueue(AppClaims app);

        ///<summary>
        /// Removes messages older than the configured max lifetime
        ///</summary>
        void RemoveOldMessagesFromQueue();

        ///<summary>
        /// Returns logs for each app in batches
        ///</summary>
        /// <param name="maxBatchSize">Max number of logs per batch</param>
        /// <param name="maxNumberOfBatches">Max number of batches to return</param>
        Dictionary<AppClaims, List<List<LogMsg>>> GetAppLogBatches(int maxBatchSize, int maxNumberOfBatches);

        ///<summary>
        /// Requeue a batch and update the failure count
        ///</summary>
        void ReQueueBatch(AppClaims app, List<LogMsg> batch);
    }
}
