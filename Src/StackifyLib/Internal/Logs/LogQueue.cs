using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using StackifyLib.Models;
using StackifyLib.Utils;

#if NET45 || NET40
using System.Runtime.Remoting.Messaging;
using StackifyLib.Web;
#endif

namespace StackifyLib.Internal.Logs
{
    internal class LogQueue
    {
        static DateTime _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private ConcurrentQueue<Models.LogMsg> _MessageBuffer = null;
        private System.Threading.Timer _timer = null;
        private TimeSpan _FlushInterval = TimeSpan.FromSeconds(1);


        private bool _StopRequested = false;
        private bool _UploadingNow = false;
        private bool _QueueTooBig = true;
        private bool _IsWebApp = false;
        private LogClient _LogClient = null;
        private bool _PauseUpload = false;
        private bool _TimerStarted = false;

        public LogQueue(LogClient logClient)
        {
            StackifyAPILogger.Log("Creating new LogQueue");

            _LogClient = logClient;
            _IsWebApp = false;// System.Web.Hosting.HostingEnvironment.IsHosted;
            _MessageBuffer = new ConcurrentQueue<LogMsg>();

        }

        public bool CanQueue()
        {
            //maximum size at which we just drop the messages
            return _QueueTooBig;
        }

        /// <summary>
        /// Doing this so the Logger class in StackifyLib doesn't run a timer unless someone actually logs something
        /// </summary>
        public void EnsureTimer()
        {
            if (_timer == null)
            {
                _TimerStarted = true;
                _timer = new System.Threading.Timer(OnTimer, null, _FlushInterval, _FlushInterval);
            }
        }

        //private struct ThreadUsage
        //{
        //    public string SrcMethod { get; set; }
        //    public string TransID { get; set; }
        //}


        //ConcurrentDictionary<string, ThreadUsage> _ThreadInfo = new ConcurrentDictionary<string, ThreadUsage>();

        /// <summary>
        /// Should call CanSend() before this. Did not also put that call in here to improve performance. Makes more sense to do it earlier so it can skip other steps up the chain.
        /// </summary>
        /// <param name="msg"></param>
        public void QueueLogMessage(Models.LogMsg msg)
        {
            try
            {
                if (msg == null)
                    return;

                if (!_TimerStarted)
                {
                    EnsureTimer();
                }


                //try
                //{

                //    if (string.IsNullOrEmpty(msg.Th))
                //    {
                //        msg.Th = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
                //    }
                //}
                //catch
                //{
                //}

#if NET45 || NET40
                try
                {
                    if (string.IsNullOrEmpty(msg.TransID))
                    {

                        Object stackifyRequestID = CallContext.LogicalGetData("Stackify-RequestID");

                        if (stackifyRequestID != null)
                        {
                            msg.TransID = stackifyRequestID.ToString();
                        }
                    }

                    if (string.IsNullOrEmpty(msg.TransID))
                    {
                        //gets from Trace.CorrelationManager.ActivityId but doesnt assume it is guid since it technically doesn't have to be
                        //not calling the CorrelationManager method because it blows up if it isn't a guid
                        Object correltionManagerId = CallContext.LogicalGetData("E2ETrace.ActivityID");

                        if (correltionManagerId != null && correltionManagerId is Guid && ((Guid)correltionManagerId) != Guid.Empty)
                        {
                            msg.TransID = correltionManagerId.ToString();
                        }
                    }

                    if (string.IsNullOrEmpty(msg.TransID))
                    {
                        if (_IsWebApp && System.Web.Hosting.HostingEnvironment.IsHosted
                                 && System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.Handler != null &&
                                 System.Web.HttpContext.Current.Request != null)
                        {
                            msg.TransID = System.Web.HttpContext.Current.Request.GetHashCode().ToString();
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



                if (_IsWebApp && System.Web.Hosting.HostingEnvironment.IsHosted
                    && System.Web.HttpContext.Current != null
                    && System.Web.HttpContext.Current.Handler != null
                    && System.Web.HttpContext.Current.Request != null)
                {
                    var context = System.Web.HttpContext.Current;

                    msg.UrlFull = context.Request.Url.ToString();

                    if (context.Items != null && context.Items.Contains("Stackify.ReportingUrl"))
                    {
                        msg.UrlRoute = context.Items["Stackify.ReportingUrl"].ToString();
                    }
                    else
                    {
                        RouteResolver resolver = new RouteResolver(context);

                        var route = resolver.GetRoute();

                        if (!string.IsNullOrEmpty(route.Action))
                        {
                            msg.UrlRoute = route.ToString();
                        }
                    }


                    if (string.IsNullOrEmpty(msg.UrlRoute))
                    {
                        HelperFunctions.CleanPartialUrl(
                            context.Request.AppRelativeCurrentExecutionFilePath.TrimStart('~'));
                    }


                }

#endif

                _MessageBuffer.Enqueue(msg);

            }
            catch
            {

            }
        }


        private void OnTimer(Object stateInfo)
        {
            if (_PauseUpload)
                return;

            _timer.Change(-1, -1); //disable while it does this so it does fire multiple times


            try
            {
                //remove messages in the queue that are old
                if (!_LogClient.IsAuthorized() && _MessageBuffer.Count > 0)
                {
                    var cutoff = (long)DateTime.UtcNow.AddMinutes(-5).Subtract(_Epoch).TotalMilliseconds;

                    while (true)
                    {
                        LogMsg msg;
                        if (_MessageBuffer.TryPeek(out msg) && msg.EpochMs < cutoff)
                        {
                            LogMsg msg2;
                            _MessageBuffer.TryDequeue(out msg2);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (_timer != null && !_StopRequested)
                        _timer.Change(_FlushInterval, _FlushInterval);

                    return;
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log(ex.ToString());
            }


            try
            {
                int processedCount = FlushLoop();

                //auto adjust how often we send based on how the rate of which data is being logged
                if (processedCount >= 100)
                {
                    if (_FlushInterval.TotalSeconds > 1)
                    {
                        _FlushInterval = TimeSpan.FromSeconds(_FlushInterval.TotalSeconds / 2);
                        StackifyLib.Utils.StackifyAPILogger.Log(
                            string.Format("Adjust log flush interval down to {0:0.00} seconds",
                                          _FlushInterval.TotalSeconds));
                    }
                }
                else if (processedCount < 10 && _FlushInterval != TimeSpan.FromSeconds(5))
                {
                    double proposedSeconds = _FlushInterval.TotalSeconds * 1.25;

                    if (proposedSeconds < 1)
                    {
                        proposedSeconds = 1;
                    }
                    else if (proposedSeconds > 5)
                    {
                        proposedSeconds = 5;
                    }

                    if (_FlushInterval.TotalSeconds < proposedSeconds)
                    {
                        _FlushInterval = TimeSpan.FromSeconds(proposedSeconds);

                        StackifyLib.Utils.StackifyAPILogger.Log(
                            string.Format("Adjust log flush interval up to {0:0.00} seconds",
                                          _FlushInterval.TotalSeconds));
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log(ex.ToString());
            }

            if (_timer != null && !_StopRequested)
                _timer.Change(_FlushInterval, _FlushInterval);
        }


        private int FlushLoop()
        {
            _UploadingNow = true;
            int processedCount = 0;
            try
            {
                int queueSize = _MessageBuffer.Count;

                // StackifyLib.Utils.StackifyAPILogger.Log("FlushLoop - count: " + queueSize + " for " + _LogClient.LoggerName);

                //CanSend() does an IdentifyApp so there is a chance this could take a while
                if (queueSize > 0 && _LogClient.CanUpload())
                {
                    _QueueTooBig = queueSize < Logger.MaxLogBufferSize;

                    bool keepGoing = false;

                    var tasks = new List<Task>();

                    int flushTimes = 0;
                    //Keep flushing
                    do
                    {
                        int count;
                        var task = FlushOnceAsync(out count);

                        if (task != null)
                        {
                            tasks.Add(task);
                        }

                        if (count >= 100)
                        {
                            keepGoing = true;
                        }
                        else
                        {
                            keepGoing = false;
                        }
                        flushTimes++;
                        processedCount += count;
                    } while (keepGoing && flushTimes < 25);


                    if (_StopRequested && tasks.Any())
                    {
                        StackifyLib.Utils.StackifyAPILogger.Log("Waiting to ensure final log send. Waiting on " + tasks.Count + " tasks");
                        Task.WaitAll(tasks.ToArray(), 5000);
                        StackifyLib.Utils.StackifyAPILogger.Log("Final log flush complete");
                    }

                    _QueueTooBig = _MessageBuffer.Count < Logger.MaxLogBufferSize;
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log(ex.ToString());
            }
            _UploadingNow = false;
            return processedCount;
        }

        private Task FlushOnceAsync(out int messageSize)
        {

            // StackifyLib.Utils.StackifyAPILogger.Log("Calling FlushOnceAsync");

            messageSize = 0;
            var chunk = new List<LogMsg>();

            //we only want to do this once at a time but the actual send is done async
            long startMs = (long)DateTime.UtcNow.Subtract(_Epoch).TotalMilliseconds;
            try
            {
                while (true)
                {
                    LogMsg msg;
                    if (_MessageBuffer.TryDequeue(out msg))
                    {
                        //do not log our own messages. This is to prevent any sort of recursion that could happen since calling to send this will cause even more logging to happen
                        if (msg.Msg != null & msg.Msg.IndexOf("StackifyLib:") > -1)
                        {
                            //skip!
                            continue;
                        }

                        chunk.Add(msg);

                        messageSize++;

                        //if we get something newer than when we started reading, break so it doesn't keep reading perpetually. 
                        //Let it finish so the timer can run again and do a new batch

                        if (msg.EpochMs > startMs)
                        {
                            break;
                        }

                        //send this packet in a batch
                        if (messageSize > 100)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (chunk.Any())
                {

                    return _LogClient.SendLogsByGroups(chunk.ToArray()).ContinueWith((continuation) =>
                    {

                        if (continuation.Exception != null)
                        {
                            Utils.StackifyAPILogger.Log("Requeueing log messages due to error: " + continuation.Exception.ToString(), true);
                        }

                        if (continuation.Result != null && continuation.Result.Exception != null)
                        {
                            Utils.StackifyAPILogger.Log("Requeueing log messages due to error: " + continuation.Result.Exception.ToString(), true);
                        }

                        if (continuation.Exception != null ||
                            (continuation.Result != null && continuation.Result.Exception != null))
                        {
                            try
                            {
                                bool messagesSentTooManyTimes = false;

                                foreach (var item in chunk)
                                {
                                    item.UploadErrors++;

                                    // try to upload up to 10 times
                                    if (item.UploadErrors < 100)
                                    {
                                        _MessageBuffer.Enqueue(item);
                                    }
                                    else
                                    {
                                        messagesSentTooManyTimes = true;
                                    }
                                }

                                if (messagesSentTooManyTimes)
                                {
                                    Utils.StackifyAPILogger.Log(
                                        "Some messages not queued again due to too many failures uploading");
                                }

                            }
                            catch (Exception ex2)
                            {
                                Utils.StackifyAPILogger.Log("Error trying to requeue messages " + ex2.ToString());
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.StackifyAPILogger.Log(ex.ToString());


                //requeue the messages that errored trying to upload
                try
                {
                    foreach (var item in chunk)
                    {
                        item.UploadErrors++;

                        // try to upload up to 10 times
                        if (item.UploadErrors < 10)
                        {
                            _MessageBuffer.Enqueue(item);
                        }
                    }

                }
                catch (Exception ex2)
                {
                    Utils.StackifyAPILogger.Log(ex2.ToString());
                }
            }


            return null;
        }


        public void Stop()
        {
            Utils.StackifyAPILogger.Log("LogQueue stop received");
            _StopRequested = true;

            if (!_UploadingNow)
            {
                FlushLoop();
            }
            else
            {
                DateTime stopWaiting = DateTime.UtcNow.AddSeconds(5);

                //wait for it to finish up to 5 seconds
                while (_UploadingNow && DateTime.UtcNow < stopWaiting)
                {
#if NET45 || NET40
                    System.Threading.Thread.Sleep(10);
#else
                    Task.Delay(10).Wait();
#endif
                }

            }
            Utils.StackifyAPILogger.Log("LogQueue stop complete");
        }

        public void Pause(bool isPaused)
        {
            _PauseUpload = isPaused;
        }
    }
}
