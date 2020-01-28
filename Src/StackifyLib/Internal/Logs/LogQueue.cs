using StackifyLib.Models;
using StackifyLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

#if NETFULL
using System.Runtime.Remoting.Messaging;
using StackifyLib.Web;
using System.Web;
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
                {
                    return;
                }

                if (!_TimerStarted)
                {
                    EnsureTimer();
                }

                try
                {

                    if (string.IsNullOrEmpty(msg.Th))
                    {
                        msg.Th = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
                    }
                }
                catch
                {
                    // ignore
                }

       
#if NETFULL
                try
                {
                    if (string.IsNullOrEmpty(msg.TransID))
                    {
                        var stackifyRequestID = CallContext.LogicalGetData("Stackify-RequestID");

                        if (stackifyRequestID != null)
                        {
                            msg.TransID = stackifyRequestID.ToString();
                        }
                    }

                    if (string.IsNullOrEmpty(msg.TransID))
                    {
                        //gets from Trace.CorrelationManager.ActivityId but doesnt assume it is guid since it technically doesn't have to be
                        //not calling the CorrelationManager method because it blows up if it isn't a guid
                        var correltionManagerId = CallContext.LogicalGetData("E2ETrace.ActivityID");

                        if (correltionManagerId != null && correltionManagerId is Guid && ((Guid)correltionManagerId) != Guid.Empty)
                        {
                            msg.TransID = correltionManagerId.ToString();
                        }
                    }

                    if (string.IsNullOrEmpty(msg.TransID))
                    {
                        if (_IsWebApp 
                            && System.Web.Hosting.HostingEnvironment.IsHosted 
                            && System.Web.HttpContext.Current != null 
                            && System.Web.HttpContext.Current.Handler != null)
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

                if (_IsWebApp 
                    && System.Web.Hosting.HostingEnvironment.IsHosted
                    && System.Web.HttpContext.Current != null
                    && System.Web.HttpContext.Current.Handler != null)
                {
                    var context = System.Web.HttpContext.Current;

                    msg.UrlFull = context.Request.Url.ToString();

                    if (context.Items.Contains("Stackify.ReportingUrl"))
                    {
                        msg.UrlRoute = context.Items["Stackify.ReportingUrl"].ToString();
                    }
                    else
                    {
                        var resolver = new RouteResolver(new HttpContextWrapper(context));

                        var route = resolver.GetRoute();

                        if (!string.IsNullOrEmpty(route.Action))
                        {
                            msg.UrlRoute = route.ToString();
                        }
                    }

                    if (string.IsNullOrEmpty(msg.UrlRoute))
                    {
                        if (string.IsNullOrWhiteSpace(context.Request.AppRelativeCurrentExecutionFilePath) == false)
                        {
                            HelperFunctions.CleanPartialUrl(context.Request.AppRelativeCurrentExecutionFilePath.TrimStart('~'));
                        }
                    }
                }
#else
                // else if .Net Core
                // get RequestID
                if (string.IsNullOrEmpty(msg.TransID))
                {
                    var q = AppDomain.CurrentDomain.GetAssemblies();

                    var s = q.Where(assembly => assembly.FullName.Contains("Stackify.Agent"));
                    if(s.Count() > 0)
                    {
                        var middleware = s.First();
                        var callContextType = middleware.GetType("Stackify.Agent.Threading.StackifyCallContext");
                        if (callContextType != null)
                        {
                            var traceCtxType = middleware.GetType("Stackify.Agent.Tracing.ITraceContext");
                            if(traceCtxType != null)
                            {
                                var traceContextProp = callContextType.GetProperty("TraceContext")?.GetValue(null);
                                if (traceContextProp != null)
                                {
                                    var reqIdProp = traceCtxType.GetProperty("RequestId")?.GetValue(traceContextProp)?.ToString();
                                    if(!string.IsNullOrEmpty(reqIdProp))
                                        msg.TransID = reqIdProp;
                                }
                            }
                        }
                    }
                }
#endif

                _MessageBuffer.Enqueue(msg);
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#LogQueue #QueueLogMessage failed", ex);
            }
        }


        private void OnTimer(Object stateInfo)
        {
            if (_PauseUpload)
            {
                return;
            }

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
                    {
                        _timer.Change(_FlushInterval, _FlushInterval);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#LogQueue #OnTimer failed", ex);
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
                        StackifyAPILogger.Log(string.Format("#LogQueue Adjust log flush interval down to {0:0.00} seconds", _FlushInterval.TotalSeconds));
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

                        StackifyAPILogger.Log(string.Format("#LogQueue Adjust log flush interval up to {0:0.00} seconds", _FlushInterval.TotalSeconds));
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#LogQueue #QueueLogMessage failed", ex);
            }

            if (_timer != null && !_StopRequested)
            {
                _timer.Change(_FlushInterval, _FlushInterval);
            }
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

                    int flushTimes = 0;
                    //Keep flushing
                    do
                    {
                        int count = FlushOnce();

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

                    _QueueTooBig = _MessageBuffer.Count < Logger.MaxLogBufferSize;
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#LogQueue #FlushLoop failed", ex);
            }
            _UploadingNow = false;
            return processedCount;
        }

        private int FlushOnce()
        {
            // StackifyLib.Utils.StackifyAPILogger.Log("Calling FlushOnceAsync");

            int messageSize = 0;
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
                        if (msg.Msg != null && msg.Msg != null && msg.Msg.Contains("StackifyLib:"))
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
                    var response = _LogClient.SendLogsByGroups(chunk.ToArray());

                    if (response != null && response.Exception != null)
                    {
                        Utils.StackifyAPILogger.Log("Requeueing log messages due to error: " + response.Exception.ToString(), true);

                        if (response.IsClientError())
                        {
                            Utils.StackifyAPILogger.Log("#LogQueue Not requeueing log messages due to client error: " + response.StatusCode, true);
                        }
                        else
                        {
                            try
                            {
                                bool messagesSentTooManyTimes = EnqueueForRetransmission(chunk);

                                if (messagesSentTooManyTimes)
                                {
                                    Utils.StackifyAPILogger.Log("#LogQueue Some messages not queued again due to too many failures uploading");
                                }
                            }
                            catch (Exception ex2)
                            {
                                Utils.StackifyAPILogger.Log("#LogQueue Error trying to requeue messages " + ex2.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#LogQueue #FlushOnce failed", ex);

                EnqueueForRetransmission(chunk);
            }

            return messageSize;
        }


        public void Stop()
        {
            Utils.StackifyAPILogger.Log("#LogQueue stop received");
            _StopRequested = true;

            if (!_UploadingNow)
            {
                FlushLoop();
            }
            else
            {
                DateTime stopWaiting = DateTime.UtcNow.AddSeconds(15);

                //wait for it to finish up to 5 seconds
                while (_UploadingNow && DateTime.UtcNow < stopWaiting)
                {
                    System.Threading.Thread.Sleep(10);
                }

            }
            Utils.StackifyAPILogger.Log("#LogQueue stop complete");
        }

        public void Pause(bool isPaused)
        {
            _PauseUpload = isPaused;
        }

        private bool EnqueueForRetransmission(List<LogMsg> chunk)
        {
            bool skippedMessage = false;

            try
            {
                foreach (var item in chunk)
                {
                    ++item.UploadErrors;

                    // retry up to 5 times
                    if (item.UploadErrors < 5)
                    {
                        _MessageBuffer.Enqueue(item);
                    }
                    else
                    {
                        skippedMessage = true;
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#LogQueue #EnqueueForRetransmission failed", ex);
            }

            return skippedMessage;
        }
    }
}
