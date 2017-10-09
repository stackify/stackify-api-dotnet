using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using StackifyLib.Models;
using StackifyLib.Utils;
using StackifyLib.Internal.Auth.Claims;

namespace StackifyLib.Internal.Logs
{
    ///<summary>
    /// Manages log queues for each publishing application
    ///</summary>
    internal class AppLogQueues : IAppLogQueues
    {
        private readonly ConcurrentDictionary<AppClaims, ConcurrentQueue<LogMsg>> _queues = new ConcurrentDictionary<AppClaims, ConcurrentQueue<LogMsg>>();
        private readonly int _maxSize;
        public bool IsFull => _queues.Sum(q => q.Value.Count) >= _maxSize;
        public bool IsEmpty => _queues.IsEmpty || _queues.All(q => q.Value.IsEmpty);

        public AppLogQueues(int maxSize)
        {
            _maxSize = maxSize;
        }

        public void QueueMessage(AppClaims app, LogMsg msg)
        {
            var queue = GetAppQueue(app);
            queue.Enqueue(msg);
        }

        public ConcurrentQueue<LogMsg> GetAppQueue(AppClaims app)
        {
            if (_queues.TryGetValue(app, out ConcurrentQueue<LogMsg> queue) == false)
            {
                queue = new ConcurrentQueue<LogMsg>();
                _queues[app] = queue;
            }

            return queue;
        }

        public void RemoveOldMessagesFromQueue()
        {
            try
            {
                if (_queues.IsEmpty) return;

                var cutoff = (long)DateTime.UtcNow.AddMinutes(-5).Subtract(StackifyConstants.Epoch).TotalMilliseconds;

                foreach (var appQueueEntry in _queues)
                {
                    var appQueue = appQueueEntry.Value;
                    while (true)
                    {
                        if (appQueue.TryPeek(out LogMsg msg) && msg.EpochMs < cutoff)
                        {
                            StackifyAPILogger.Log($"Removed old message");
                            appQueue.TryDequeue(out LogMsg oldMessage);
                        }
                        else
                        {
                            StackifyAPILogger.Log($"Not removing");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
            }
        }
        
        public Dictionary<AppClaims, List<List<LogMsg>>> GetAppLogBatches(int maxBatchSize, int maxNumberOfBatches)
        {
            var appBatches = new  Dictionary<AppClaims, List<List<LogMsg>>>();
            foreach(var appQueueDefinition in _queues)
            {
                appBatches.Add(
                    appQueueDefinition.Key, 
                    GetBatchesFromQueue(appQueueDefinition.Value, maxBatchSize, maxNumberOfBatches));
            }
            return appBatches;
        }

        private List<List<LogMsg>> GetBatchesFromQueue(ConcurrentQueue<LogMsg> queue, int maxBatchSize, int maxNumberOfBatches)
        {
            StackifyAPILogger.Log($"Getting batches");

            var batches = new List<List<LogMsg>>();

            while (batches.Count < maxNumberOfBatches && queue.Count > 0)
            {
                var batch = new List<LogMsg>();
                while (batch.Count < maxBatchSize && queue.TryDequeue(out LogMsg msg))
                {
                    var msgContent = msg.Msg ?? string.Empty;
                    if (msgContent.Contains("StackifyLib:") == false)
                    {
                        batch.Add(msg);
                    }
                }

                if (batch.Count > 0)
                {
                    batches.Add(batch);
                }
            }

            StackifyAPILogger.Log($"Returning batch size {batches.Count}");
            return batches;
        }

        public void ReQueueBatch(AppClaims app, List<LogMsg> batch)
        {
            try
            {
                if(IsFull) return;
                    
                const int maxQueueAttempts = 10;
                foreach (var log in batch)
                {
                    log.UploadErrors++;

                    // try to upload up to 10 times
                    if (log.UploadErrors < maxQueueAttempts)
                    {
                        _queues[app].Enqueue(log);
                    }
                    else
                    {
                        StackifyAPILogger.Log("Some messages not queued again due to too many failures uploading");
                    }
                }
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log("Error trying to requeue messages " + e.ToString());
            }
        }
    }
}
