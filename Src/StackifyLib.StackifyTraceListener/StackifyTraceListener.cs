using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib
{
    public class StackifyTraceListener : TraceListener
    {
        private StackifyLib.Internal.Logs.ILogClient _logClient = null;

        public StackifyTraceListener()
        {
            _logClient = LogClientFactory.GetClient("StackifyLib.net-TraceListener");
        }


        private void WriteToStackify(string level, string message)
        {
            try
            {

                //make sure the buffer isn't overflowing
                //if it is skip since we can't do anything with the message

                if (Logger.PrefixEnabled() || _logClient.CanQueue())
                {

                    LogMsg msg = new LogMsg();
                    msg.Msg = message;
                    msg.Level = level;

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

        public override void WriteLine(object o, string category)
        {
            base.WriteLine(o, category);
        }

        public override void WriteLine(string message, string category)
        {
            WriteToStackify("TRACE", message);
        }

        public override void WriteLine(object o)
        {
            base.WriteLine(o);
        }

        public override void Write(string message)
        {
            WriteToStackify("TRACE", message);
        }

        public override void WriteLine(string message)
        {
            WriteToStackify("TRACE", message);
        }

        public override void Fail(string message)
        {
            WriteToStackify("FAIL", message);
        }

        public override void Fail(string message, string detailMessage)
        {
            WriteToStackify("FAIL", message);
        }

        public override void Flush()
        {
            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                StackifyAPILogger.Log("TraceListener closing");

                _logClient.Close();

                //This is to force the metrics queue to flush as well
                StackifyLib.Internal.Metrics.MetricClient.StopMetricsQueue("TraceListener OnClose");
            }
            catch
            {


            }

            base.Dispose(disposing);
        }
    }
}
