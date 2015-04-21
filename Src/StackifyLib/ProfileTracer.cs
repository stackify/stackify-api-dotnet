using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace StackifyLib
{
    public class ProfileTracer
    {
        private string _methodDisplayText = null;
        private bool ignoreChildFrames = false;

        private string _requestReportingCategory = null;
        private string _appReportingCategory = null;

        private bool _customMetricCount = false;
        private bool _customMetricTime = false;
        private bool _autoReportZeroIfNothingReported = false;

        private string _customMetricCategory = null;
        private string _customMetricName = null;

        private string _transactionID = Guid.NewGuid().ToString();
        private string _RequestID = null;

        public ProfileTracer(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory)
        {
            _methodDisplayText = methodDisplayText;
            _requestReportingCategory = requestLevelReportingCategory;
            _appReportingCategory = appLevelReportingCategory;



            try
            {
                if (System.Web.HttpContext.Current != null)
                {
                    var id = System.Web.HttpContext.Current.Items["Stackify-RequestID"];

                    if (id != null && !string.IsNullOrEmpty(id.ToString()))
                    {
                        _RequestID = id.ToString();
                    }
                }

                if (string.IsNullOrEmpty(_RequestID))
                {
                    Object correltionManagerId = CallContext.LogicalGetData("Stackify-RequestID");

                    if (correltionManagerId != null)
                    {
                        _RequestID = correltionManagerId.ToString();
                    }
                }
            }
            catch
            {

            }

         
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void SetReportingUrl(string reportingUrl)
        {
            try
            {
                if (System.Web.HttpContext.Current != null)
                {
                    System.Web.HttpContext.Current.Items["Stackify.ReportingUrl"] = reportingUrl;
                }
            }
            catch 
            {
                
            }
     
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TraceString(string value)
        {

        }

        public static ProfileTracer Create(string methodDisplayText)
        {
            ProfileTracer tracer = new ProfileTracer(methodDisplayText, null, null);
            return tracer;
        }

        public static ProfileTracer Create(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory = null)
        {
            ProfileTracer tracer = new ProfileTracer(methodDisplayText, requestLevelReportingCategory, appLevelReportingCategory);
            return tracer;
        }


        public ProfileTracer CreateMetric(string categoryName, string metricName, bool trackCount = true, bool trackTime = true, bool autoReportZeroIfNothingReported = false)
        {
            _customMetricCategory = categoryName;
            _customMetricName = metricName;
            _customMetricCount = trackCount;
            _customMetricTime = trackTime;
            _autoReportZeroIfNothingReported = autoReportZeroIfNothingReported;

            return this;
        }

        public ProfileTracer IgnoreChildFrames(bool value = true)
        {
            ignoreChildFrames = value;
            return this;
        }

        //Method the profiler looks for
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternal(string traceDisplay, int suppressChildren, string requestLevelReportingCategory, string appLevelReportingCategory, string actionID, string requestID, Action action)
        {
            action();
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private Task ExecInternal(string traceDisplay, int suppressChildren, string requestLevelReportingCategory, string appLevelReportingCategory, string actionID, string requestID, Func<Task> action)
        {
            Task t = action();
            ExecInternalTaskStarted(actionID, t.Id);
            return t;
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalTaskStarted(string actionID, int taskID)
        {

        }


        public void Exec(Action action)
        {
            if (action == null)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ExecInternal(_methodDisplayText, ignoreChildFrames ? 1 : 0, _requestReportingCategory, _appReportingCategory, _transactionID, _RequestID, action);
  

            if (_customMetricTime)
            {
                Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
            }


            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }

            ExecInternalComplete(_transactionID, _RequestID);
        }



        public Task ExecAsync(Func<Task> task)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            var t = ExecInternal(_methodDisplayText, ignoreChildFrames ? 1 : 0, _requestReportingCategory, _appReportingCategory, _transactionID, _RequestID, task);

            t.ContinueWith((tend) =>
            {
                if (_customMetricTime)
                {
                    Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
                }
                ExecInternalComplete(_transactionID, _RequestID, tend.Id);
            });

            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }

            return t;
        }


        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalComplete(string actionID, string requestID)
        {

        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalComplete(string actionID, string requestID, int taskID)
        {
            
        }

     
    }
}
