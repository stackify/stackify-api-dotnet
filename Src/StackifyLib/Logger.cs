using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib
{
    public class Logger
    {
        public static int _MaxLogBufferSize = 10000;
       // public static bool AutoSetTransID = false;

        private static LogClient _LogClient = null;

        static Logger()
        {
            _LogClient = new LogClient("StackifyLib.net");
        }



        public static string GlobalAppName = null;
        public static string GlobalEnvironment = null;
        public static string GlobalApiKey = null;

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
            _LogClient.Close();
        }

        public static AppIdentityInfo Identity()
        {
            return _LogClient.GetIdentity();
        }

        public static bool CanSend()
        {
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
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(debugData);
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
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(debugData);
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

        public static void QueueLogObject(StackifyLib.Models.LogMsg msg, Exception exceptionObject)
        {
            try
            {
                if (_LogClient.CanQueue())
                {
                    if (exceptionObject != null)
                    {
                        var error = StackifyError.New(exceptionObject);
                        
                        if (!StackifyError.IgnoreError(error) && _LogClient.ErrorShouldBeSent(error))
                        {
                            msg.Ex = error;

                            if (!string.IsNullOrEmpty(msg.Msg))
                            {
                                msg.Ex.SetAdditionalMessage(msg.Msg);
                            }

                            msg.Msg = msg.Ex.ToString();
                        }
                        else
                        {
                            msg.Msg += " #errorgoverned";
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


        public static List<TraceFrame> GetCurrentStackTrace(string declaringClassName)
        {
            List<TraceFrame> frames = new List<TraceFrame>();

            try
            {
                //moves to the part of the trace where the declaring method starts then the other loop gets all the frames. This is to remove frames that happen within the logging library itself.
                StackTrace stackTrace = new StackTrace(true);
                int index1;
                for (index1 = 0; index1 < stackTrace.FrameCount; ++index1)
                {
                    var frame = stackTrace.GetFrame(index1);

                    if (frame != null)
                    {
                        var method = frame.GetMethod();

                        if (method != null && method.DeclaringType != null && method.DeclaringType.FullName == declaringClassName)
                        {
                            break;
                        }
                    }

                }

                if (index1 < stackTrace.FrameCount)
                {

                    for (int index2 = index1; index2 < stackTrace.FrameCount; ++index2)
                    {
                        var frame2 = stackTrace.GetFrame(index2);
                        var f2 = new TraceFrame();
                        f2.CodeFileName = frame2.GetFileName();
                        f2.LineNum = frame2.GetFileLineNumber();
                        f2.Method = ErrorItem.GetMethodFullName(frame2.GetMethod());
                        frames.Add(f2);
                    }

                }
            }
            catch (Exception ex)
            {

            }

            return frames;
        }

    }
}
