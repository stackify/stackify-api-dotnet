using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.Logs;
using StackifyLib.Internal.Metrics;
using StackifyLib.Models;
using StackifyLib.Utils;

#if NETFULL
using System.Diagnostics;
#endif

namespace StackifyLib
{
    public class Logger
    {
        private static ILogClient _logClient;

        static Logger()
        {
            _logClient = LogClientFactory.GetClient("StackifyLib.net");
        }

        /// <summary>
        /// Callback when logs are not uploaded to the API and the library will not retry
        /// </summary>
        public static event Action<List<LogMsg>, HttpStatusCode> OnRejectedLogs;

        internal static void NotifyRejectedLogs(List<LogMsg> logs, HttpStatusCode status) => OnRejectedLogs?.Invoke(logs, status);

        /// <summary>
        /// Flushes any items in the queue when shutting down an app
        /// </summary>
        public static void Shutdown()
        {
            //flush logs queue
            _logClient.Close();

            //flush any remaining metrics as well
            MetricClient.StopMetricsQueue("Logger Shutdown called");
        }

        /// <summary>
        /// Used to check if there have been recent failures or if the queue is backed up and if logs can be sent or not
        /// </summary>
        public static bool CanSend()
        {
            if (_logClient == null)
                return false;

            return _logClient.CanQueue();
        }

        public static void Queue(string level, string message, object debugData = null)
        {
            var msg = new LogMsg()
            {
                Level = level,
                Msg = message
            };

            if (debugData != null)
            {
                //set json data to pass through as debugging data
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(debugData, true);
            }

            QueueLogObject(msg, null);
        }

        public static void QueueException(Exception exceptionObject,
                                          object debugData = null)
        {
            QueueException("ERROR", exceptionObject.Message, exceptionObject, debugData);
        }

        public static void QueueException(string message, Exception exceptionObject,
                                          object debugData = null)
        {
            QueueException("ERROR", message, exceptionObject, debugData);
        }

        public static void QueueException(string level, string message, Exception exceptionObject, object debugData = null)
        {
            var msg = new LogMsg()
            {
                Level = level,
                Msg = message
            };

            if (debugData != null)
            {
                //set json data to pass through as debugging data
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(debugData, true);
            }

            QueueLogObject(msg, exceptionObject);
        }

        internal static bool ErrorShouldBeSent(StackifyError error)
        {
            return _logClient.ErrorShouldBeSent(error);
        }

        public static void PauseUpload(bool isPaused)
        {
            _logClient.PauseUpload(isPaused);
        }

        public static void QueueException(StackifyError error)
        {
            var msg = new LogMsg()
            {
                Level = "ERROR",
                Msg = error.Message,
                Ex = error
            };

            QueueLogObject(msg);
        }

        public static void QueueLogObject(LogMsg msg)
        {
            try
            {
                if (PrefixEnabled() || _logClient.CanQueue())
                {

                    if (msg.Ex != null)
                    {
                        if (string.IsNullOrEmpty(msg.Level))
                        {
                            msg.Level = "ERROR";
                        }

                        string origMsg = msg.Msg;

                        if (msg.Msg != null && msg.Ex != null)
                        {
                            msg.Msg += "\r\n" + msg.Ex.ToString();
                        }
                        else if (msg.Msg == null && msg.Ex != null)
                        {
                            msg.Msg = msg.Ex.ToString();
                        }


                        bool ignore = StackifyError.IgnoreError(msg.Ex);
                        bool shouldSend = _logClient.ErrorShouldBeSent(msg.Ex);

                        if (!ignore)
                        {
                            if (!string.IsNullOrEmpty(origMsg))
                            {
                                msg.Ex.SetAdditionalMessage(origMsg);
                            }

                            if (!shouldSend)
                            {
                                msg.Ex = null;
                                msg.Msg += " #errorgoverned";
                            }
                        }
                        else
                        {
                            msg.Ex = null;
                        }
                    }

                    _logClient.QueueMessage(msg);
                }
                else
                {
                    StackifyAPILogger.Log("Unable to send log because the queue is full");
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
            }
        }

        public static void QueueLogObject(LogMsg msg, Exception exceptionObject)
        {
            if (exceptionObject != null)
            {
                msg.Ex = StackifyError.New(exceptionObject);
            }

            QueueLogObject(msg);
        }

        /// <summary>
        /// Helper method for getting the current stack trace
        /// </summary>
        /// <param name="declaringClassName"></param>
        public static List<TraceFrame> GetCurrentStackTrace(string declaringClassName, int maxFrames = 99, bool simpleMethodNames = false)
        {
            List<TraceFrame> frames = new List<TraceFrame>();

#if NETFULL
            try
            {
                //moves to the part of the trace where the declaring method starts then the other loop gets all the frames. This is to remove frames that happen within the logging library itself.
                StackTrace stackTrace = new StackTrace(true);
                int index1;
                var stackTraceFrames = stackTrace.GetFrames();
                for (index1 = 0; index1 < stackTraceFrames.Length; ++index1)
                {
                    var frame = stackTraceFrames[index1];

                    if (frame != null)
                    {
                        var method = frame.GetMethod();

                        if (method != null && method.DeclaringType != null && method.DeclaringType.FullName == declaringClassName)
                        {
                            break;
                        }
                    }

                }

                if (index1 < stackTraceFrames.Length)
                {

                    for (int index2 = index1; index2 < stackTraceFrames.Length; ++index2)
                    {
                        var frame2 = stackTraceFrames[index2];
                        var f2 = new TraceFrame();
                        f2.CodeFileName = frame2.GetFileName();
                        f2.LineNum = frame2.GetFileLineNumber();
                        f2.Method = ErrorItem.GetMethodFullName(frame2.GetMethod(), simpleMethodNames);
                        frames.Add(f2);

                        if (frames.Count > maxFrames)
                        {
                            return frames;
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
#endif
            return frames;
        }

        public static bool PrefixEnabled()
        {
            return PrefixOrAPM.GetProfilerType() == PrefixOrAPM.ProfilerType.Prefix;
        }
    }
}
