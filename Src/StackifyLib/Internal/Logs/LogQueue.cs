using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using StackifyLib.Models;
using StackifyLib.Utils;

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
        private bool _CanSend = true;
        private bool _IsWebApp = false;
        private LogClient _LogClient = null;
        private bool _PauseUpload = false;

        public LogQueue(LogClient logClient)
        {
            StackifyAPILogger.Log("Creating new LogQueue");

            _LogClient = logClient;
            _IsWebApp = System.Web.Hosting.HostingEnvironment.IsHosted;
            _MessageBuffer = new ConcurrentQueue<LogMsg>();
            _timer = new System.Threading.Timer(OnTimer, null, _FlushInterval, _FlushInterval);
        }

        public bool CanSend()
        {
            //maximum size at which we just drop the messages
            return _CanSend;
        }
        


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

                try
                {
                    if (string.IsNullOrEmpty(msg.TransID))
                    {
                        //gets from Trace.CorrelationManager.ActivityId but doesnt assume it is guid since it technically doesn't have to be
                        //not calling the CorrelationManager method because it blows up if it isn't a guid
                        Object correltionManagerId = CallContext.LogicalGetData("E2ETrace.ActivityID");

                        if (correltionManagerId != null)
                        {
                            msg.TransID = correltionManagerId.ToString();
                        }
                        else 
                            if (_IsWebApp && System.Web.Hosting.HostingEnvironment.IsHosted
                            && System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.Request != null)
                        {
                            msg.TransID = System.Web.HttpContext.Current.Request.GetHashCode().ToString();
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    //Thread # for the OS, not .net
                    if (string.IsNullOrEmpty(msg.ThOs))
                    {
                        msg.ThOs = AppDomain.GetCurrentThreadId().ToString();
                    }
                }
                catch
                {
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
                }

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
                int processedCount = FlushLoop();

                //auto adjust how often we send based on how the rate of which data is being logged
                if (processedCount >= 100)
                {
                    if (_FlushInterval.TotalSeconds > 1)
                    {
                        _FlushInterval = TimeSpan.FromSeconds(_FlushInterval.TotalSeconds/2);
                        StackifyLib.Utils.StackifyAPILogger.Log(
                            string.Format("Adjust log flush interval down to {0:0.00} seconds",
                                          _FlushInterval.TotalSeconds));
                    }
                }
                else if(processedCount < 10 && _FlushInterval != TimeSpan.FromSeconds(5))
                {
                    double proposedSeconds = _FlushInterval.TotalSeconds*1.25;

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

                //CanSend() does an IdentifyApp so there is a chance this could take a while
                if (queueSize > 0 && _LogClient.CanSend())
                {
                    _CanSend = queueSize < Logger.MaxLogBufferSize;

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
                    } while (keepGoing && !_StopRequested && flushTimes < 25);

                    _CanSend = _MessageBuffer.Count < Logger.MaxLogBufferSize;
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log(ex.ToString());
            }
            _UploadingNow = false;
            return processedCount;
        }

        private int FlushOnce()
        {
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
                    _LogClient.SendLogs(chunk.ToArray());
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


            return messageSize;
        }


        public void Stop()
        {
            Utils.StackifyAPILogger.Log("LogQueue stop received");
            _StopRequested = true;

            if (!_UploadingNow)
            {
                FlushLoop();
            }
        }

        public void Pause(bool isPaused)
        {
            _PauseUpload = isPaused;
        }
    }
}
