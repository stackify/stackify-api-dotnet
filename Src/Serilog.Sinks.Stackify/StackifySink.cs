
using System;
using System.IO;
using Serilog.Core;
using Serilog.Events;
using StackifyLib;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace Serilog.Sinks.Stackify
{
    public class StackifySink : ILogEventSink, IDisposable
    {
        private readonly IFormatProvider _formatProvider;
        private readonly JsonDataFormatter _dataFormatter;

        private LogClient _logClient = null;


        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        public StackifySink(IFormatProvider formatProvider)
        {
            _formatProvider = formatProvider;

            _dataFormatter = new JsonDataFormatter();

            _logClient = new LogClient("StackifyLib.net-serilog", null, null);


            AppDomain.CurrentDomain.DomainUnload += OnAppDomainUnloading;
            AppDomain.CurrentDomain.ProcessExit += OnAppDomainUnloading;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnloading;
        }

        private void OnAppDomainUnloading(object sender, EventArgs args)
        {
            var exceptionEventArgs = args as UnhandledExceptionEventArgs;
            if (exceptionEventArgs != null && !exceptionEventArgs.IsTerminating)
                return;
            CloseAndFlush();
        }


        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            try
            {
                //make sure the buffer isn't overflowing
                //if it is skip since we can't do anything with the message
                if (Logger.PrefixEnabled() || _logClient.CanQueue())
                {
                    var logMsg = Translate(logEvent);
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

        internal LogMsg Translate(LogEvent loggingEvent)
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



            if (loggingEvent.HasStackTrace && loggingEvent.UserStackFrame != null)
            {
                var frame = loggingEvent.UserStackFrame;

                MethodBase method = frame.GetMethod();
                if (method != (MethodBase)null && method.DeclaringType != (Type)null)
                {
                    if (method.DeclaringType != (Type)null)
                    {
                        msg.SrcMethod = method.DeclaringType.FullName + "." + method.Name;
                        msg.SrcLine = frame.GetFileLineNumber();
                    }
                }

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

            string formattedMessage;

            //Use the layout render to allow custom fields to be logged, but not if it is the default format as it logs a bunch fields we already log
            //really no reason to use a layout at all
            if (this.Layout != null && this.Layout.ToString() != "'${longdate}|${level:uppercase=true}|${logger}|${message}'") //do not use if it is the default
            {
                formattedMessage = this.Layout.Render(loggingEvent);
            }
            else
            {
                formattedMessage = loggingEvent.FormattedMessage;
            }

            msg.Msg = (formattedMessage ?? "").Trim();

            object debugObject = null;
            Dictionary<string, object> args = new Dictionary<string, object>();

            if ((loggingEvent.Parameters != null) && (loggingEvent.Parameters.Length > 0))
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
                    else if (item.ToString() == msg.Msg)
                    {
                        //ignore it.   
                    }
                    else if (logAllParams ?? true)
                    {
                        args["arg" + i] = loggingEvent.Parameters[i];
                        debugObject = item;
                    }
                    else
                    {
                        debugObject = item;
                    }
                }

                if ((logAllParams ?? true) && args != null && args.Count > 1)
                {
                    debugObject = args;
                }
            }


            StackifyError error = null;

            if (loggingEvent.Exception != null && loggingEvent.Exception is StackifyError)
            {
                error = (StackifyError)loggingEvent.Exception;
            }
            else if (loggingEvent.Exception != null)
            {
                error = StackifyError.New((Exception)loggingEvent.Exception);
            }

            var diags = GetDiagnosticContextProperties();
            if (diags != null && diags.ContainsKey("transid"))
            {
                msg.TransID = diags["transid"].ToString();
                diags.Remove("transid");
            }








            if (debugObject != null)
            {
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(debugObject, true, diags);
            }
            else
            {
                msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(null, false, diags);
            }


            if (msg.Msg != null && error != null)
            {
                msg.Msg += "\r\n" + error.ToString();
            }
            else if (msg.Msg == null && error != null)
            {
                msg.Msg = error.ToString();
            }

            if (error == null && (loggingEvent.Level == LogLevel.Error || loggingEvent.Level == LogLevel.Fatal))
            {
                StringException stringException = new StringException(msg.Msg);

                stringException.TraceFrames = StackifyLib.Logger.GetCurrentStackTrace(loggingEvent.LoggerName);

                if (!loggingEvent.HasStackTrace || loggingEvent.UserStackFrame == null)
                {
                    if (stringException.TraceFrames.Any())
                    {
                        var first = stringException.TraceFrames.First();

                        msg.SrcMethod = first.Method;
                        msg.SrcLine = first.LineNum;
                    }
                }

                //Make error out of log message
                error = StackifyError.New(stringException);
            }

            if (error != null && !StackifyError.IgnoreError(error) && _logClient.ErrorShouldBeSent(error))
            {
                error.SetAdditionalMessage(formattedMessage);
                msg.Ex = error;
            }
            else if (error != null && msg.Msg != null)
            {
                msg.Msg += " #errorgoverned";
            }


            return msg;
        }


        private string PropertiesToData(LogEvent logEvent)
        {
            var payload = new StringWriter();
            _dataFormatter.FormatData(logEvent, payload);

            return payload.ToString();
        }



        static string LevelToSeverity(LogEvent logEvent)
        {
            switch (logEvent.Level)
            {
                case LogEventLevel.Debug:
                    return "DEBUG";
                case LogEventLevel.Error:
                    return "ERROR";
                case LogEventLevel.Fatal:
                    return "FATAL";
                case LogEventLevel.Verbose:
                    return "VERBOSE";
                case LogEventLevel.Warning:
                    return "WARNING";
                default:
                    return "INFORMATION";
            }
        }

        private void CloseAndFlush()
        {
            try
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Serilog target closing");
                _logClient.Close();
                StackifyLib.Internal.Metrics.MetricClient.StopMetricsQueue("Serilog CloseTarget");
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Serilog target closing error: " + ex.ToString());
            }

            AppDomain.CurrentDomain.DomainUnload -= OnAppDomainUnloading;
            AppDomain.CurrentDomain.ProcessExit -= OnAppDomainUnloading;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnloading;

           
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            CloseAndFlush();
            _disposed = true;
        }
    }
}
