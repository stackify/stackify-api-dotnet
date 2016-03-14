using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;
using Apache_log4net = log4net;
using log4net.Appender;
using log4net.spi;
using log4netOffical = log4net;


namespace StackifyLib.log4net.Sitecore
{
    public class StackifyAppender : AppenderSkeleton
    {
        public string apiKey { get; set; }
        public string uri { get; set; }
        public string globalContextKeys { get; set; }
        public string threadContextKeys { get; set; }
        public string logicalThreadContextKeys { get; set; }
        public string callContextKeys { get; set; }
        public bool? logMethodNames { get; set; }

        //private List<string> _GlobalContextKeys = new List<string>();
        //private List<string> _ThreadContextKeys = new List<string>();
        //private List<string> _LogicalThreadContextKeys = new List<string>();
        private List<string> _CallContextKeys = new List<string>();


        private ILogClient _logClient = null;
        private bool _HasContextKeys = false;

        public Func<string, string, ILogClient> CreateLogClient = (apiKey, uri) => new LogClient("StackifyLib.net-log4net-sitecore", apiKey, uri);

        public override void OnClose()
        {
            try
            {
                StackifyAPILogger.Log("log4net appender closing");

                _logClient.Close();

                //This is to force the metrics queue to flush as well
                StackifyLib.Internal.Metrics.MetricClient.StopMetricsQueue("log4net OnClose");
            }
            catch
            {


            }
        }

        public override void ActivateOptions()
        {

            StackifyAPILogger.Log("ActiveOptions on log4net appender");

            try
            {
                //if (!String.IsNullOrEmpty(globalContextKeys))
                //{
                //    _GlobalContextKeys = globalContextKeys.Split(',').Select(s => s.Trim()).ToList();
                //}

                //if (!String.IsNullOrEmpty(threadContextKeys))
                //{
                //    _ThreadContextKeys = threadContextKeys.Split(',').Select(s => s.Trim()).ToList();
                //}

                //if (!String.IsNullOrEmpty(logicalThreadContextKeys))
                //{
                //    _LogicalThreadContextKeys = logicalThreadContextKeys.Split(',').Select(s => s.Trim()).ToList();
                //}

                if (!String.IsNullOrEmpty(callContextKeys))
                {
                    _CallContextKeys = callContextKeys.Split(',').Select(s => s.Trim()).ToList();
                }

//                _HasContextKeys = _GlobalContextKeys.Any() || _ThreadContextKeys.Any() || _LogicalThreadContextKeys.Any() || _CallContextKeys.Any();
                _HasContextKeys = _CallContextKeys.Any();

                _logClient = CreateLogClient(apiKey, uri);
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.Message, true);
            }

          

            base.ActivateOptions();

        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            try
            {

                //make sure the buffer isn't overflowing
                //if it is skip since we can't do anything with the message
                
                if (Logger.PrefixEnabled() || _logClient.CanQueue())
                {
                    var logMsg = Translate(loggingEvent);
                    if (logMsg != null)
                    {
                        _logClient.QueueMessage(logMsg);
                    }
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

        internal LogMsg Translate(LoggingEvent loggingEvent)
        {
            
            if (loggingEvent == null)
                return null;

            //don't log our own logging causing a loop
            if (loggingEvent.RenderedMessage.IndexOf("StackifyLib:", StringComparison.OrdinalIgnoreCase) > -1)
                return null;


            var msg = new LogMsg();


            if (loggingEvent.Level != null)
            {
                msg.Level = loggingEvent.Level.Name;
            }
            
            try
            {
                msg.SrcMethod = loggingEvent.LoggerName;

                if (logMethodNames ?? false)
                {
                    var frames = StackifyLib.Logger.GetCurrentStackTrace(loggingEvent.LoggerName, 1, true);

                    if (frames.Any())
                    {
                        var first = frames.First();

                        msg.SrcMethod = first.Method;
                        msg.SrcLine = first.LineNum;
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error evaluating source method " + ex.ToString());
            }

            var diags = GetDiagnosticContextProperties();


            if (diags != null && diags.ContainsKey("transid"))
            {
                msg.TransID = diags["transid"].ToString();
                diags.Remove("transid");
            }

            StackifyError error = null;
            object messageObject = null;
            string errorAdditionalMessage = null;
            if (loggingEvent.MessageObject != null && loggingEvent.MessageObject is StackifyError)
            {
                //Message Object was an exception

                error = (StackifyError)loggingEvent.MessageObject;
                messageObject = error.ToString();
                errorAdditionalMessage = null;
            }
            else if (loggingEvent.MessageObject != null && loggingEvent.MessageObject is Exception)
            {
                //Message Object was an exception

                error = StackifyError.New((Exception)loggingEvent.MessageObject);
                messageObject = error.ToString();
                errorAdditionalMessage = null;

            }
            //else if (loggingEvent.ExceptionObject != null)
            //{
            //    //Exception was passed in

            //    if (loggingEvent.ExceptionObject is StackifyError)
            //    {
            //        error = loggingEvent.ExceptionObject as StackifyError;
            //    }
            //    else
            //    {
            //        error = StackifyError.New(loggingEvent.ExceptionObject);
            //    }

            //    errorAdditionalMessage = loggingEvent.RenderedMessage;
            //    messageObject = loggingEvent.MessageObject;
            //}
            else
            {
				// we try to retrieve the thrown exception but wait to do this check until this last condition
				// block since we need to use reflection which is an expensive operation
				Exception thrownException = GetThrownException(loggingEvent);

				if (thrownException == null)
				{
					// the thrown exception does not exist, so we use the basic information provided in the message object
					messageObject = loggingEvent.MessageObject;
				}
				else
				{
					if (thrownException is StackifyError)
					{
						error = thrownException as StackifyError;
					}
					else
					{
						error = StackifyError.New(thrownException);
					}

					errorAdditionalMessage = loggingEvent.RenderedMessage;
					messageObject = loggingEvent.MessageObject;
				}
			}


			//messageObject is not an object we need to serialize.
			if (messageObject == null || messageObject is string || messageObject.GetType().FullName == "log4net.Util.SystemStringFormat")
            {
                //passing null to the serialize object since we can't serialize the logged object. We only need to get potential diags.
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(null, false, diags);
                msg.Msg = loggingEvent.RenderedMessage;
            }
            else if (messageObject is StackifyLib.Models.LogMessage)
            {
                var item = messageObject as StackifyLib.Models.LogMessage;

                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(item.json, true, diags);
                msg.Msg = item.message;
            }
            else
            {
                //try to serialize the messageObject since we know its not a string
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(messageObject, false, diags);
                msg.Msg = loggingEvent.RenderedMessage;

                if (error != null)
                {
                    msg.Msg += "\r\n" + error.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(errorAdditionalMessage) && error != null)
            {
                //if something besides just the exception was logged, add that message to our error object
                error.SetAdditionalMessage(errorAdditionalMessage);
            }
            else if (msg.Msg == null && error != null)
            {
                msg.Msg = error.ToString();
            }

            if (error == null && (loggingEvent.Level == Level.ERROR || loggingEvent.Level == Level.FATAL))
            {
                StringException stringEx = new StringException(loggingEvent.RenderedMessage);
                stringEx.TraceFrames = new List<TraceFrame>();


                stringEx.TraceFrames = StackifyLib.Logger.GetCurrentStackTrace(loggingEvent.LoggerName);

                if (stringEx.TraceFrames.Any())
                {
                    var first = stringEx.TraceFrames.First();

                    msg.SrcMethod = first.Method;
                    msg.SrcLine = first.LineNum;
                }

                //Make error out of log message
                error = StackifyError.New(stringEx);
            }

            if (error != null && !StackifyError.IgnoreError(error) && _logClient.ErrorShouldBeSent(error))
            {
                msg.Ex = error;
            }
            else if(error != null && msg.Msg != null)
            {
                msg.Msg += " #errorgoverned";
            }
            return msg;
        }

		private Exception GetThrownException(LoggingEvent loggingEvent)
		{
			// Since Sitecore's implementation does not expose the ExceptionObject, we need to use reflection to get access
			// to it, as per Matt Watson from Stackify. We know they store this Exception in a field with the name m_thrownException
			// so we retrieve the private field based on its name.
			Exception thrownException = null;
			Type loggingEventType = loggingEvent.GetType();
			var thrownExceptionFieldInfo = loggingEventType.GetField("m_thrownException", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			if (thrownExceptionFieldInfo != null)
			{
				thrownException = thrownExceptionFieldInfo.GetValue(loggingEvent) as Exception;
			}

			return thrownException;
		}

		private Dictionary<string, object> GetDiagnosticContextProperties()
        {
            if (!_HasContextKeys)
            {
                return null;
            }

            
            Dictionary<string, object> properties = new Dictionary<string, object>();


            //foreach (string gdcKey in _GlobalContextKeys)
            //{
            //    object gdcValue = Apache_log4net.GlobalContext.Properties[gdcKey];

            //    if (gdcValue != null)
            //    {
            //        properties[gdcKey.ToLower()] = gdcValue;
            //    }
            //}

            //foreach (string mdcKey in _ThreadContextKeys)
            //{
            //    object mdcValue = Apache_log4net.ThreadContext.Properties[mdcKey];

            //    if (mdcValue != null)
            //    {
            //        // Check if the property is a Log4net Context Stack,
            //        // If it is, then we want to read it as a string as that is
            //        // the expected behavior for a Log4Net Appender.
            //        var tcs = mdcValue as Apache_log4net.Util.ThreadContextStack;
            //        if (tcs != null)
            //        {
            //            properties[mdcKey.ToLower()] = tcs.ToString();
            //        }
            //        else
            //        {
            //            // for anything else, we will let Json.Net take care of it.
            //            properties[mdcKey.ToLower()] = mdcValue;
            //        }
            //    }
            //}

            //foreach (string mdcKey in _LogicalThreadContextKeys)
            //{
            //    object mdcValue = Apache_log4net.LogicalThreadContext.Properties[mdcKey];

            //    if (mdcValue != null)
            //    {
            //        // Check if the property is a Log4net Context Stack,
            //        // If it is, then we want to read it as a string as that is
            //        // the expected behavior for a Log4Net Appender.

            //        var tcs = mdcValue as Apache_log4net.Util.ThreadContextStack;
            //        if (tcs != null)
            //        {
            //            properties[mdcKey.ToLower()] = tcs.ToString();
            //            continue;
            //        }

            //        // Log4Net 1.2.14 (2.0.4) added LogicalThreadContextStack
            //        // see https://issues.apache.org/jira/browse/LOG4NET-455
            //        // To maintain compatibility with version 2.0.0 in this library, we have to use reflection here.

            //        // TODO: Require version 1.2.14, as we know there are issues in prior versions.
            //        var mdcType = mdcValue.GetType();
            //        if (mdcType.FullName == "log4net.Util.LogicalThreadContextStack")
            //        {
            //            properties[mdcKey.ToLower()] = mdcValue.ToString();
            //            continue;
            //        }

            //        // for anything else, we will let Json.Net take care of it.
            //        properties[mdcKey.ToLower()] = mdcValue;
            //    }
            //}
            

            foreach (string key in _CallContextKeys)
            {
                object value = CallContext.LogicalGetData(key);

                if (value != null)
                {
                    properties[key.ToLower()] = value;
                }
            }

            return properties;
        }

    }
}
