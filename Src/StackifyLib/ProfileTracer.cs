using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;

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

            Object correltionManagerId = CallContext.LogicalGetData("Stackify-RequestID");

            if (correltionManagerId != null)
            {
                _RequestID = correltionManagerId.ToString();
            }
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

        public void Exec(Action action)
        {
            string actionID = _transactionID;
            string requestID = _RequestID;

            ExecInternal(action, _methodDisplayText, ignoreChildFrames ? 1 : 0, _requestReportingCategory, _appReportingCategory, actionID, requestID);
         
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalComplete(string actionID, string requestID)
        {
            
        }

        //Method the profiler looks for
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternal(Action action, string traceDisplay, int suppressChildren, string requestLevelReportingCategory, string appLevelReportingCategory, string actionID, string requestID)
        {

            if (_customMetricTime)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                action();

                Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
            }
            else
            {
                action();
            }

            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }

            ExecInternalComplete(actionID, requestID);
        }
    }
}
