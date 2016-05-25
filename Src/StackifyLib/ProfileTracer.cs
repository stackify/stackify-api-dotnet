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
        internal bool IsOperation { get; set; }

        internal ProfileTracer(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory)
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
        public static void SetOperationName(string operationName)
        {
           

        }


        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TraceString(string logMsg)
        {

        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TraceMongoCommand(string logMsg)
        {

        }

        [Obsolete("Use CreateAsCodeBlock or CreateAsOperation")]
        public static ProfileTracer Create(string methodDisplayText)
        {
            ProfileTracer tracer = new ProfileTracer(methodDisplayText, null, null);
            return tracer;
        }

        [Obsolete("Use CreateAsCodeBlock or CreateAsOperation")]
        public static ProfileTracer Create(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory = null)
        {
            ProfileTracer tracer = new ProfileTracer(methodDisplayText, requestLevelReportingCategory, appLevelReportingCategory);
            return tracer;
        }


        public static ProfileTracer CreateAsCodeBlock(string methodDisplayText)
        {
            ProfileTracer tracer = new ProfileTracer(methodDisplayText, null, null);
            return tracer;
        }

        public static ProfileTracer CreateAsOperation(string operationName, string uniqueOperationID = null)
        {
            ProfileTracer tracer = new ProfileTracer(operationName, null, null);
            tracer.IsOperation = true;

            if (!string.IsNullOrEmpty(uniqueOperationID))
            {
                tracer._transactionID = uniqueOperationID;
            }

            return tracer;
        }

        public static ProfileTracer CreateAsCodeBlock(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory = null)
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
        private void ExecInternal2(string values, Action action)
        {
            try
            {
                action();
            }
            finally
            {
                ExecInternalComplete2(_transactionID + "|" + _RequestID + "|0|" + IsOperation);

            }
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private Task ExecInternal2(string values, Func<Task> action)
        {
            Task t = action();
            ExecInternalTaskStarted2(_transactionID + "|" + t.Id +  "|" + IsOperation);
            return t;
        }



        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalTaskStarted2(string values)
        {

        }


        public void Exec(Action action)
        {
            if (action == null)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ExecInternal2(_methodDisplayText + "|" + (ignoreChildFrames ? 1 : 0).ToString() + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _RequestID + "|" + IsOperation, action);
  

            if (_customMetricTime)
            {
                Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
            }


            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }

        }



        public Task ExecAsync(Func<Task> task)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            var t = ExecInternal2(_methodDisplayText + "|" + (ignoreChildFrames ? 1 : 0).ToString() + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _RequestID + "|" + IsOperation, task);

            t.ContinueWith((tend) =>
            {
                if (_customMetricTime)
                {
                    Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
                }
                ExecInternalComplete2(_transactionID + "|" + _RequestID + "|" + tend.Id + "|" + IsOperation);
            });

            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }

            return t;
        }


        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalComplete2(string values)
        {

        }

    }
}
