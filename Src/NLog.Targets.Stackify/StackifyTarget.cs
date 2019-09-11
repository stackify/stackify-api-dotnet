using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Common;
using NLog.Targets;
using StackifyLib;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;
using NLog.Layouts;

namespace NLog.Targets.Stackify
{
    [Target("StackifyTarget")]
    public class StackifyTarget : TargetWithContext 
    {
        public string apiKey { get; set; }
        public string uri { get; set; }
        [Obsolete("Instead use Target ContextProperty with GDC")]
        public string globalContextKeys { get; set; }
        [Obsolete("Instead use Target ContextProperty with MDLC")]
        public string mappedContextKeys { get; set; }
        [Obsolete("Instead use Target ContextProperty with MDLC")]
        public string callContextKeys { get; set; }
        public bool? logMethodNames { get; set; }
        public bool? logAllParams { get; set; }
        [Obsolete("Instead use IncludeEventProperties property")]
        public bool? logAllProperties { get; set; }
        public bool? logLastParameter { get; set; }

        private List<string> _CallContextKeys = new List<string>();

        private LogClient _logClient = null;

        [ArrayParameter(typeof(TargetPropertyWithContext), "contextproperty")]
        public override IList<TargetPropertyWithContext> ContextProperties { get; } = new List<TargetPropertyWithContext>();

        public Layout StackifyHttpRequestInfo { get; set; }

        public StackifyTarget()
        {
            Layout = "${message}${onexception:${newline}${exception:format=tostring}}";
            IncludeEventProperties = true;
            OptimizeBufferReuse = true;
        }

        protected override void CloseTarget()
        {
            try
            {
                StackifyLib.Utils.StackifyAPILogger.Log("NLog target closing");
                _logClient.Close();
                StackifyLib.Internal.Metrics.MetricClient.StopMetricsQueue("NLog CloseTarget");
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "StackifyTarget: Failed to Close");
                StackifyLib.Utils.StackifyAPILogger.Log("NLog target closing error: " + ex.ToString());
            }
        }

        protected override void InitializeTarget()
        {
            StackifyLib.Utils.StackifyAPILogger.Log("NLog InitializeTarget");

            _logClient = new LogClient("StackifyLib.net-nlog", apiKey, uri);

#pragma warning disable CS0618 // Type or member is obsolete
            if (logAllProperties == false)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                IncludeEventProperties = false;
            }

            if (logMethodNames == true)
            {
                IncludeCallSite = true;
            }

            if (ContextProperties.Count == 0)
            {
                ContextProperties.Add(new TargetPropertyWithContext() { Name = "ndc", Layout = "${ndc:topFrames=1}" });

#pragma warning disable CS0618 // Type or member is obsolete
                var globalContextKeyList = globalContextKeys?.Split(',').Select(s => s.Trim()) ?? Enumerable.Empty<string>();

                foreach (var gdcKey in globalContextKeyList)
                {
                    if (string.IsNullOrEmpty(gdcKey))
                        continue;
                    ContextProperties.Add(new TargetPropertyWithContext() { Name = gdcKey, Layout = $"${{gdc:item={gdcKey}}}" });
                }

                var mappedContextKeyList = mappedContextKeys?.Split(',').Select(s => s.Trim()) ?? Enumerable.Empty<string>();
                foreach (var mdcKey in mappedContextKeyList)
                {
                    if (string.IsNullOrEmpty(mdcKey))
                        continue;
                    ContextProperties.Add(new TargetPropertyWithContext() { Name = mdcKey, Layout = $"${{mdc:item={mdcKey}}}" });
                }

                if (!String.IsNullOrEmpty(callContextKeys))
                {
                    _CallContextKeys = callContextKeys.Split(',').Select(s => s.Trim()).ToList();
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            try
            {
                //make sure the buffer isn't overflowing
                //if it is skip since we can't do anything with the message
                if (StackifyLib.Logger.PrefixEnabled() || _logClient.CanQueue())
                {
                    var logMsg = Translate(logEvent.LogEvent);
                    if (logMsg != null)
                    {
                        _logClient.QueueMessage(logMsg);
                    }
                    logEvent.Continuation(null); // Signal success to NLog
                }
                else
                {
                    InternalLogger.Warn("StackifyTarget: Cannot send because queue is full");
                    logEvent.Continuation(new OperationCanceledException("StackifyTarget: Cannot send because queue is full")); // Signal failure to NLog
                    StackifyAPILogger.Log("Unable to send log because the queue is full");
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error("StackifyTarget: Failed to send");
                logEvent.Continuation(ex);  // Signal failure to NLog
                StackifyAPILogger.Log(ex.ToString());
            }
        }

        internal LogMsg Translate(LogEventInfo loggingEvent)
        {
            if (loggingEvent == null)
                return null;

            //do not log our own messages. This is to prevent any sort of recursion that could happen since calling to send this will cause even more logging to happen
            if (loggingEvent.FormattedMessage != null && loggingEvent.FormattedMessage.IndexOf("StackifyLib:", StringComparison.OrdinalIgnoreCase) > -1)
                return null;

            StackifyLib.Models.LogMsg msg = new LogMsg();

            if (loggingEvent.Level != null)
            {
                msg.Level = loggingEvent.Level.Name;
            }

            if (!string.IsNullOrEmpty(loggingEvent.CallerMemberName))
            {
                msg.SrcMethod = loggingEvent.CallerMemberName;
                msg.SrcLine = loggingEvent.CallerLineNumber;
            }

            //if it wasn't set above for some reason we will do it this way as a fallback
            if (string.IsNullOrEmpty(msg.SrcMethod))
            {
                msg.SrcMethod = loggingEvent.LoggerName;

                if ((logMethodNames ?? false))
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

            msg.Msg = RenderLogEvent(Layout, loggingEvent) ?? string.Empty;

            var contextProperties = GetContextProperties(loggingEvent);
            Dictionary<string, object> diags = contextProperties as Dictionary<string, object> ?? new Dictionary<string, object>(contextProperties);
            if (diags.TryGetValue("ndc", out var topFrame) && string.IsNullOrEmpty((topFrame as string)))
            {
                diags.Remove("ndc");
            }

            if (diags.TryGetValue("transid", out var transId))
            {
                msg.TransID = diags["transid"].ToString();
                diags.Remove("transid");
            }

#if NETFULL
            foreach (string key in _CallContextKeys)
            {
                object value = System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(key);
                if (value != null)
                {
                    diags[key.ToLower()] = value;
                }
            }
#endif

            StackifyError stackifyError = loggingEvent.Exception != null ? StackifyError.New(loggingEvent.Exception) : null;
            object debugObject = null;
            if (IncludeEventProperties && loggingEvent.HasProperties)
            {
                Dictionary<string, object> args = new Dictionary<string, object>(loggingEvent.Properties.Count);
                foreach (KeyValuePair<object, object> eventProperty in loggingEvent.Properties)
                {
                    string propertyKey = eventProperty.Key.ToString();
                    if (!string.IsNullOrEmpty(propertyKey))
                    {
                        args[propertyKey] = eventProperty.Value;
                    }
                }

                if ((logAllParams ?? false) && loggingEvent.Parameters != null && loggingEvent.Parameters.Length > 0)
                {
                    debugObject = CaptureParameters(loggingEvent, msg.Msg, args);
                }
                else
                {
                    debugObject = args;
                }
            }

            if (loggingEvent.Parameters?.Length > 0)
            {
                for (int i = 0; i < loggingEvent.Parameters.Length; i++)
                {
                    var parameter = loggingEvent.Parameters[i];
                    if (stackifyError == null && parameter is StackifyError error)
                    {
                        stackifyError = error;
                    }
                    if (debugObject == null && parameter is LogMessage debugMessage)
                    {
                        debugObject = debugMessage.json;
                    }
                }

                if (debugObject == null)
                {
                    Dictionary<string, object> args = ((logAllParams ?? true) && loggingEvent.Parameters.Length > 1) ? new Dictionary<string, object>() : null;
                    debugObject = CaptureParameters(loggingEvent, msg.Msg, args);
                }
            }

            if (debugObject != null)
            {
                msg.data = HelperFunctions.SerializeDebugData(debugObject, true, diags);
            }
            else
            {
                msg.data = HelperFunctions.SerializeDebugData(null, false, diags);
            }

            if (stackifyError == null && (loggingEvent.Level == LogLevel.Error || loggingEvent.Level == LogLevel.Fatal))
            {
                StringException stringException = new StringException(msg.Msg);
                if ((logMethodNames ?? false))
                {
                    stringException.TraceFrames = StackifyLib.Logger.GetCurrentStackTrace(loggingEvent.LoggerName);
                }
                stackifyError = StackifyError.New(stringException);
            }

            string stackifyHttp = StackifyHttpRequestInfo.Render(loggingEvent);
            if (stackifyError.WebRequestDetail == null && !String.IsNullOrEmpty(stackifyHttp))
            {
#if NETFULL
                var webRequestDetail = Newtonsoft.Json.JsonConvert.DeserializeObject<WebRequestDetail>(stackifyHttp);
                stackifyError.WebRequestDetail = webRequestDetail;
#endif
            }

            if (stackifyError != null && !StackifyError.IgnoreError(stackifyError) && _logClient.ErrorShouldBeSent(stackifyError))
            {
                stackifyError.SetAdditionalMessage(loggingEvent.FormattedMessage);
                msg.Ex = stackifyError;
            }
            else if (stackifyError != null && msg.Msg != null)
            {
                msg.Msg += " #errorgoverned";
            }

            return msg;
        }

        private object CaptureParameters(LogEventInfo loggingEvent, string logMessage, Dictionary<string, object> args)
        {
            object debugObject = null;
            if (args != null || (logLastParameter ?? true))
            {
                for (int i = 0; i < loggingEvent.Parameters.Length; i++)
                {
                    var item = loggingEvent.Parameters[i];

                    if (item == null)
                    {
                        continue;
                    }
                    else if (item is Exception)
                    {
                        if (loggingEvent.Exception == null)
                        {
                            loggingEvent.Exception = (Exception)item;
                        }
                    }
                    else if (item.ToString() == logMessage)
                    {
                        //ignore it.   
                    }
                    else if (args != null)
                    {
                        args["arg" + i] = item;
                        debugObject = item;
                    }
                    else
                    {
                        debugObject = item;
                    }
                }

                if (args != null && args.Count > 1)
                {
                    debugObject = args;
                }
            }

            return debugObject;
        }
    }
}
