using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using StackifyLib.Internal.Logs;
using StackifyLib.Internal.Metrics;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib
{
    public class Logger
    {
        public static int _MaxLogBufferSize = 10000;

        private static LogClient _LogClient = null;

        static Logger()
        {
            _LogClient = new LogClient("StackifyLib.net");
        }


        /// <summary>
        /// Used to override the appname being used by any and all logging appenders. Be it this Logger class, log4net, NLog, etc
        /// </summary>
        [Obsolete("Use StackifyLib.Config instead", true)]
        public static string GlobalAppName = null;

        /// <summary>
        /// Used to override the environment being used by any and all logging appenders. Be it this Logger class, log4net, NLog, etc
        /// </summary>
        [Obsolete("Use StackifyLib.Config instead", true)]
        public static string GlobalEnvironment = null;

        /// <summary>
        /// Used to override the api key being used by any and all logging appenders. Be it this Logger class, log4net, NLog, etc
        /// </summary>
        [Obsolete("Use StackifyLib.Config instead", true)]
        public static string GlobalApiKey = null;


        /// <summary>
        /// Used to get/set the api key used by this logger class, not appenders like log4net, NLog, etc. Set GlobalApiKey to change it for those
        /// </summary>
        [Obsolete("Use StackifyLib.Config instead", true)]
        public static string ApiKey
        {
            get
            {
                return _LogClient.APIKey;
            }
            set
            {
                //close the log client and create a new one with the new key
                //people shouldn't really be changing this around so not worried about it being crazily set
                if (_LogClient.APIKey != value)
                {
                    _LogClient.Close();
                    _LogClient = new LogClient("StackifyLib.net", value);
                }
            }
        }

        /// <summary>
        /// Global setting for any log appenders for how big the log queue size can be in memory before messages are lost if there are problems uploading or we can't upload fast enough
        /// </summary>
        public static int MaxLogBufferSize
        {
            get { return _MaxLogBufferSize; }
            set { _MaxLogBufferSize = value; }
        }

        /// <summary>
        /// Flushes any items in the queue when shutting down an app
        /// </summary>
        public static void Shutdown()
        {
            //flush logs queue
            _LogClient.Close();

            //flush any remaining metrics as well
            MetricClient.StopMetricsQueue("Logger Shutdown called");
        }

        /// <summary>
        /// Gets info about the app
        /// </summary>
        /// <returns></returns>
        public static AppIdentityInfo Identity()
        {
            return _LogClient.GetIdentity();
        }

        /// <summary>
        /// Used to check if there have been recent failures or if the queue is backed up and if logs can be sent or not
        /// </summary>
        /// <returns></returns>
        public static bool CanSend()
        {
            if (_LogClient == null)
                return false;

            return _LogClient.CanQueue();
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
            return _LogClient.ErrorShouldBeSent(error);
        }

        public static void PauseUpload(bool isPaused)
        {
            _LogClient.PauseUpload(isPaused);
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

        public static void QueueLogObject(StackifyLib.Models.LogMsg msg)
        {
            try
            {
                if (PrefixEnabled() || _LogClient.CanQueue())
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
                        bool shouldSend = _LogClient.ErrorShouldBeSent(msg.Ex);


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

                    _LogClient.QueueMessage(msg);
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

        public static void QueueLogObject(StackifyLib.Models.LogMsg msg, Exception exceptionObject)
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
        /// <param name="maxFrames"></param>
        /// <param name="simpleMethodNames"></param>
        /// <returns></returns>
        public static List<TraceFrame> GetCurrentStackTrace(string declaringClassName, int maxFrames = 99, bool simpleMethodNames = false)
        {
            var frames = new List<TraceFrame>();

#if NETFULL
            try
            {
                //moves to the part of the trace where the declaring method starts then the other loop gets all the frames. This is to remove frames that happen within the logging library itself.
                var stackTrace = new StackTrace(true);
                var stackTraceFrames = stackTrace.GetFrames();
                if (stackTraceFrames != null)
                {
                    int index1;

                    for (index1 = 0; index1 < stackTraceFrames.Length; ++index1)
                    {
                        var frame = stackTraceFrames[index1];

                        if (frame != null)
                        {
                            var method = frame.GetMethod();

                            if (method != null && method.DeclaringType != null &&
                                method.DeclaringType.FullName == declaringClassName)
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
            }
            catch
            {
                // ignored
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
