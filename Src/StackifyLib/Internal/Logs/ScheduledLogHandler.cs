using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackifyLib.Models;
using StackifyLib.Utils;
using System.Threading;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.Scheduling;
using System.Net;

#if NETFULL
using System.Runtime.Remoting.Messaging;
using StackifyLib.Web;
#endif

namespace StackifyLib.Internal.Logs
{
    internal class ScheduledLogHandler : IScheduledLogHandler
    {
        private readonly IAppLogQueues _appQueues;
        private readonly IStackifyApiService _stackifyApiService;
        private readonly IScheduler _scheduler;
        private TimeSpan _flushInterval = TimeSpan.FromSeconds(2);
        private bool _stopRequested = false;
        private bool _pauseUpload = false;

        public ScheduledLogHandler(
            IStackifyApiService apiService,
            IScheduler scheduler,
            IAppLogQueues appQueues)
        {
            StackifyAPILogger.Log("Creating new LogQueue");
            _stackifyApiService = apiService;
            _scheduler = scheduler;
            _appQueues = appQueues;
        }

        public bool CanQueue()
        {
            var canQueue = _appQueues.IsFull == false;

            if (canQueue == false)
                StackifyAPILogger.Log($"Cannot queue message");

            return canQueue;
        }

        /// <summary>
        /// Should call CanQueue() before this.
        /// </summary>
        public void QueueLogMessage(AppClaims app, LogMsg msg)
        {
            try
            {
                if (msg == null)
                    return;

                if (_scheduler.IsStarted == false)
                {
                    _scheduler.Schedule(OnTimerAsync, _flushInterval);
                }

                msg.Th = GetThread(msg);
                msg.TransID = GetTransaction(msg);

                _appQueues.QueueMessage(app, msg);
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log($"Failed to add message to the queue. { ex.Message }");
            }
        }

        private string GetThread(LogMsg msg)
        {
            try
            {
                return string.IsNullOrEmpty(msg.Th)
                    ? Thread.CurrentThread.ManagedThreadId.ToString()
                    : msg.Th;
            }
            catch { }
            return string.Empty;
        }

        private string GetTransaction(LogMsg msg)
        {
            return string.IsNullOrWhiteSpace(msg.TransID)
                    ? GetTransId()
                    : msg.TransID;
        }

        private string GetTransId()
        {
            var transId = string.Empty;

#if NETFULL
            try
            {
                Object stackifyRequestID = CallContext.LogicalGetData("Stackify-RequestID");

                if (stackifyRequestID != null)
                {
                    transId = stackifyRequestID.ToString();
                }

                if (string.IsNullOrEmpty(transId))
                {
                    // gets from Trace.CorrelationManager.ActivityId but doesnt assume it is guid since it technically doesn't have to be
                    // not calling the CorrelationManager method because it blows up if it isn't a guid
                    Object correltionManagerId = CallContext.LogicalGetData("E2ETrace.ActivityID");

                    if (correltionManagerId != null && correltionManagerId is Guid && ((Guid)correltionManagerId) != Guid.Empty)
                    {
                        transId = correltionManagerId.ToString();
                    }
                }
            }
            catch (System.Web.HttpException ex)
            {
                StackifyAPILogger.Log("Request not available \r\n" + ex.ToString());
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error figuring out TransID \r\n" + ex.ToString());
            }
            
#endif

            return transId;
        }

        private async void OnTimerAsync(Object stateInfo)
        {
            if (_pauseUpload)
                return;

            _scheduler.Pause();

            _appQueues.RemoveOldMessagesFromQueue();

            try
            {
                var processedCount = await FlushAllQueuesAsync();

                UpdateFlushInterval(processedCount);
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
            }

            if (_stopRequested == false)
            {
                _scheduler.Change(_flushInterval);
                StackifyAPILogger.Log($"Resetting Timer");
            }
        }

        ///<summary>
        /// Adjust how often we send based on how the rate of which data is being logged
        ///</summary>
        private void UpdateFlushInterval(int processedCount)
        {
            if (processedCount >= 100)
            {
                if (_flushInterval.TotalSeconds > 1)
                {
                    _flushInterval = TimeSpan.FromSeconds(_flushInterval.TotalSeconds / 2);
                    StackifyAPILogger.Log($"{processedCount} processed. Adjust log flush interval down to {_flushInterval.TotalSeconds:0.00} seconds");
                }
            }
            else if (processedCount < 10 && _flushInterval != TimeSpan.FromSeconds(5))
            {
                double proposedSeconds = _flushInterval.TotalSeconds * 1.25;

                if (proposedSeconds < 1)
                {
                    proposedSeconds = 1;
                }
                else if (proposedSeconds > 5)
                {
                    proposedSeconds = 5;
                }

                if (_flushInterval.TotalSeconds < proposedSeconds)
                {
                    _flushInterval = TimeSpan.FromSeconds(proposedSeconds);

                    StackifyAPILogger.Log($"{processedCount} processed. Adjust log flush interval up to {_flushInterval.TotalSeconds:0.00} seconds");
                }
            }
        }

        private async Task<int> FlushAllQueuesAsync()
        {
            StackifyAPILogger.Log("FlushAllQueuesAsync");

            var totalMessagesProcessed = 0;

            var appLogBatches = _appQueues.GetAppLogBatches(100, 25);
            foreach (var appLogBatch in appLogBatches)
            {
                totalMessagesProcessed += await FlushQueueInBatchesAsync(appLogBatch.Key, appLogBatch.Value);
            }
            return totalMessagesProcessed;
        }

        private async Task<int> FlushQueueInBatchesAsync(AppClaims app, List<List<LogMsg>> batches)
        {
            StackifyAPILogger.Log("FlushQueueInBatchesAsync");
            var totalMessagesProcessed = 0;
            try
            {
                if (batches.Count > 0)
                {
                    var tasks = new List<Task<int>>();

                    // keep flushing
                    foreach (var batch in batches)
                        tasks.Add(FlushOnceAsync(app, batch));

                    StackifyAPILogger.Log($"{tasks.Count} batches in flight", true);

                    var processedCounts = await Task.WhenAll(tasks);
                    totalMessagesProcessed = processedCounts.Sum();

                    StackifyAPILogger.Log("FlushQueueInBatchesAsync complete", true);
                }
                else
                {
                    StackifyAPILogger.Log("No messages in queue", true);
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
            }

            return totalMessagesProcessed;
        }

        private async Task<int> FlushOnceAsync(AppClaims app, List<LogMsg> batch)
        {
            var result = (int)await SendLogGroupAsync(app, batch);

            if (IsSuccessStatusCode(result))
                return batch.Count;

            StackifyAPILogger.Log("Failed to send log group");

            if (ShouldReQueue(result))
                _appQueues.ReQueueBatch(app, batch);
            else
                Logger.NotifyRejectedLogs(batch, (HttpStatusCode)result);

            return 0;
        }

        private async Task<HttpStatusCode> SendLogGroupAsync(AppClaims app, List<LogMsg> messages)
        {
            try
            {
                StackifyAPILogger.Log("Trying to SendLogs");

                var group = new LogMsgGroup
                {
                    Msgs = messages
                };

                StackifyAPILogger.Log($"Sending {messages.Count} log messages.");

                var statusCode = await _stackifyApiService.UploadAsync(app, GetRequestUri(messages.Count), group, compress: true);

                return statusCode;
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log($"Failed to send logs due to {ex}");
                return 0;
            }
        }

        private string GetRequestUri(int numberOfLogs) => $"{Config.LogUri}/{numberOfLogs}";

        private bool IsSuccessStatusCode(int statusCode)
            => (statusCode >= 200 && statusCode < 300);

        private bool ShouldReQueue(int status)
        {
            if (status == 0) // request was not sent
                return false;

            if (status < 400)
                return true;

            // do not re-queue for client errors
            if (status >= 400 && status < 500)
                return false;

            return true;
        }

        public async Task Stop()
        {
            _scheduler.Pause();
            StackifyAPILogger.Log($"LogQueue stop received from:\n{Environment.StackTrace}");
            _stopRequested = true;
            await FlushAllQueuesAsync();
        }

        public void Pause(bool isPaused)
        {
            _pauseUpload = isPaused;
        }
    }
}
