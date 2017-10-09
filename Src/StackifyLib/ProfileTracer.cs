using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using StackifyLib.Utils;

namespace StackifyLib
{
    public class ProfileTracer
    {
        private readonly string _methodDisplayText = null;
        private bool _ignoreChildFrames = false;

        private readonly string _requestReportingCategory = null;
        private readonly string _appReportingCategory = null;

        private bool _customMetricCount = false;
        private bool _customMetricTime = false;
        private bool _autoReportZeroIfNothingReported = false;

        private string _customMetricCategory = null;
        private string _customMetricName = null;

        private readonly string _transactionID = Guid.NewGuid().ToString();
        private string _requestId = null;

        internal bool IsOperation { get; set; }

#if NET45 || NET451 || NETSTANDARD1_3
        private static EtwEventListener _etwEventListener = null;
#endif

        internal ProfileTracer(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory)
        {
            _methodDisplayText = methodDisplayText;
            _requestReportingCategory = requestLevelReportingCategory;
            _appReportingCategory = appLevelReportingCategory;

#if NETFULL
            try
            {
                if (System.Web.HttpContext.Current != null)
                {
                    var id = System.Web.HttpContext.Current.Items["Stackify-RequestID"];

                    if (id != null && !string.IsNullOrEmpty(id.ToString()))
                    {
                        _requestId = id.ToString();
                    }
                }

                if (string.IsNullOrEmpty(_requestId))
                {
                    Object correltionManagerId = System.Runtime.Remoting.Messaging.CallContext.LogicalGetData("Stackify-RequestID");

                    if (correltionManagerId != null)
                    {
                        _requestId = correltionManagerId.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#ProfileTracer ctor", ex);
            }
#endif
         
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void SetReportingUrl(string reportingUrl)
        {
#if NETFULL
            try
            {
                if (System.Web.HttpContext.Current != null)
                {
                    System.Web.HttpContext.Current.Items["Stackify.ReportingUrl"] = reportingUrl;
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#ProfileTracer #SetReportingUrl", ex);
            }
#endif
        }


        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void SetOperationName(string operationName)
        {
           

        }

        [Obsolete("Just used for testing", false)]
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void NoOp()
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

        /// <summary>
        /// Used to logically group a section of code
        /// </summary>
        /// <param name="methodDisplayText"></param>
        /// <returns></returns>
        public static ProfileTracer CreateAsCodeBlock(string methodDisplayText)
        {
            var tracer = new ProfileTracer(methodDisplayText, null, null);
            return tracer;
        }

        /// <summary>
        /// Used by non web apps to define transactions in code that are turned in to operations to be tracked in Stackify APM or Prefix
        /// </summary>
        /// <param name="functionName"></param>
        /// <returns></returns>
        public static ProfileTracer CreateAsTrackedFunction(string functionName)
        {
            var tracer = new ProfileTracer(functionName, "Tracked Function", null);
            return tracer;
        }

        /// <summary>
        /// Used by non web apps to define transactions in code that are turned in to operations to be tracked in Stackify APM or Prefix
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="uniqueOperationID"></param>
        /// <returns></returns>
        public static ProfileTracer CreateAsOperation(string operationName, string uniqueOperationID = null)
        {
#if NET45 || NET451 || NETSTANDARD1_3
            if (_etwEventListener == null)
            {
                _etwEventListener = new EtwEventListener();
            }
#endif

            var tracer = new ProfileTracer(operationName, null, null);
            tracer.IsOperation = true;

            if (!string.IsNullOrEmpty(uniqueOperationID))
            {
                tracer._requestId = uniqueOperationID;
            }

            return tracer;
        }

        /// <summary>
        /// Used to logically group a section of code
        /// </summary>
        /// <param name="methodDisplayText"></param>
        /// <param name="requestLevelReportingCategory"></param>
        /// <param name="appLevelReportingCategory"></param>
        /// <returns></returns>

        public static ProfileTracer CreateAsDependency(string methodDisplayText, string requestLevelReportingCategory, string appLevelReportingCategory = null)
        {
            var tracer = new ProfileTracer(methodDisplayText, requestLevelReportingCategory, appLevelReportingCategory);
            return tracer;
        }


        public ProfileTracer SetUniqueOperationID(string uniqueOperationID)
        {
            if (!string.IsNullOrEmpty(uniqueOperationID))
            {
                this._requestId = uniqueOperationID;
            }

            return this;
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
            _ignoreChildFrames = value;
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
                ExecInternalComplete2(_transactionID + "|" + _requestId + "|0|" + IsOperation);
            }
        }


        //Method the profiler looks for
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalOperation(string values, Action action)
        {
            try
            {
                action();
            }
            finally
            {
                ExecInternalComplete2(_transactionID + "|" + _requestId + "|0|" + IsOperation);
            }
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private Task ExecInternal2(string values, Func<Task> action)
        {
            var t = action();
            ExecInternalTaskStarted2(_transactionID + "|" + t.Id +  "|" + IsOperation);
            return t;
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private Task<T> ExecInternal2<T>(string values, Func<Task<T>> action)
        {
            var t = action();
            ExecInternalTaskStarted2(_transactionID + "|" + t.Id + "|" + IsOperation);
            return t;
        }


        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private Task ExecInternalOperation(string values, Func<Task> action)
        {
            var t = action();
            ExecInternalTaskStarted2(_transactionID + "|" + t.Id + "|" + IsOperation);
            return t;
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private Task<T> ExecInternalOperation<T>(string values, Func<Task<T>> action)
        {
            var t = action();
            ExecInternalTaskStarted2(_transactionID + "|" + t.Id + "|" + IsOperation);
            return t;
        }


        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExecInternalTaskStarted2(string values)
        {

        }


        public void Exec(Action action)
        {
            if (action == null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;

            if (this.IsOperation)
            {
                ExecInternalOperation(_methodDisplayText + "|" + (_ignoreChildFrames ? 1 : 0) + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _requestId + "|" + IsOperation, action);
            }
            else
            {
                ExecInternal2(_methodDisplayText + "|" + (_ignoreChildFrames ? 1 : 0) + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _requestId + "|" + IsOperation, action);

            }

            if (_customMetricTime)
            {
                Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
            }

            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }
        }

        public Task<T> ExecAsync<T>(Func<Task<T>> task)
        {
            var now = DateTimeOffset.UtcNow;

            Task<T> t;

            if (this.IsOperation)
            {
                t = ExecInternalOperation<T>(_methodDisplayText + "|" + (_ignoreChildFrames ? 1 : 0) + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _requestId + "|" + IsOperation, task);
            }
            else
            {
                t = ExecInternal2<T>(_methodDisplayText + "|" + (_ignoreChildFrames ? 1 : 0) + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _requestId + "|" + IsOperation, task);
            }

            t.ContinueWith((tend) =>
            {
                if (_customMetricTime)
                {
                    Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
                }
                ExecInternalComplete2(_transactionID + "|" + _requestId + "|" + tend.Id + "|" + IsOperation);
            });

            if (_customMetricCount)
            {
                Metrics.Count(_customMetricCategory, _customMetricName, 1, _autoReportZeroIfNothingReported);
            }

            return t;
        }

        public Task ExecAsync(Func<Task> task)
        {
            var now = DateTimeOffset.UtcNow;

            Task t;

            if (this.IsOperation)
            {
                t = ExecInternalOperation(_methodDisplayText + "|" + (_ignoreChildFrames ? 1 : 0) + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _requestId + "|" + IsOperation, task);
            }
            else
            {
                t = ExecInternal2(_methodDisplayText + "|" + (_ignoreChildFrames ? 1 : 0) + "|" + _requestReportingCategory + "|" + _appReportingCategory + "|" + _transactionID + "|" + _requestId + "|" + IsOperation, task);
            }

            t.ContinueWith((tend) =>
            {
                if (_customMetricTime)
                {
                    Metrics.Time(_customMetricCategory, _customMetricName + " Time", now);
                }
                ExecInternalComplete2(_transactionID + "|" + _requestId + "|" + tend.Id + "|" + IsOperation);
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