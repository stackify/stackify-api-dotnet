using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using StackifyLib.Utils;
using System.Threading.Tasks;
using StackifyLib.Internal.Logs;
using System.Threading;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Models;

#if NETFULL
using System.Web;
#endif

namespace StackifyLib
{
    [JsonObject]
    public class StackifyError : Exception
    {

        [JsonProperty]
        public long OccurredEpochMillis { get; set; }

        //our version of the error details
        [JsonProperty]
        public ErrorItem Error { get; set; }

        //Details of the device generating the error
        [JsonProperty]
        public AppClaims EnvironmentDetail { get; set; }

        [JsonProperty]
        public WebRequestDetail WebRequestDetail { get; set; }

        [JsonProperty]
        public Dictionary<string, string> ServerVariables { get; set; }

        [JsonProperty]
        public string CustomerName { get; set; }
        
        [JsonProperty]
        public string UserName { get; set; }

        public delegate void CapturDetailHandler(StackifyError ex);

        public static event CapturDetailHandler OnCaptureDetail;

        //internally kept reference to the exception
        [JsonIgnore]
        private Exception _Exception = null;

        [JsonIgnore]
        internal bool _Sent { get; set; }

        [JsonIgnore]
        private LogMsg _InternalLogMsg { get; set; }

        [JsonIgnore]
        public bool IsUnHandled { get; set; }

        // required for de-serialization
        public StackifyError()
        { }

        public StackifyError(long errorOccurredEpochMillis, ErrorItem errorItem)
            : this(errorItem.Message, null)
        {
            OccurredEpochMillis = errorOccurredEpochMillis;
            this.Error = errorItem;
        }

        public StackifyError(DateTime errorOccurredUtc, ErrorItem errorItem)
            : this(errorItem.Message, null)
        {
            TimeSpan ts = errorOccurredUtc.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0));
            OccurredEpochMillis = (long)ts.TotalMilliseconds;
            this.Error = errorItem;
        }

        public StackifyError(Exception exception)
            : this(exception.Message, exception)
        {

        }

        public StackifyError(string message, Exception exception)
            : base(message)
        {
            Init();

            if (exception != null)
            {
                //Reflection caused error. Real error is the inner exception
                if (exception is TargetInvocationException && exception.InnerException != null)
                {
                    Error = new ErrorItem(exception.InnerException);
                    _Exception = exception.InnerException;
                }
#if NETFULL
                else if (exception is HttpUnhandledException && exception.InnerException != null)
                {
                    Error = new ErrorItem(exception.GetBaseException());
                    _Exception = exception.GetBaseException();
                }
#endif
                else
                {
                    Error = new ErrorItem(exception);
                    _Exception = exception;
                }
            }

            SetAdditionalMessage(message);
        }

        private void Init()
        {
            TimeSpan ts = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0));
            OccurredEpochMillis = (long)ts.TotalMilliseconds;

            EnvironmentDetail = AppClaimsManager.Get();
            ServerVariables = new Dictionary<string, string>();

#if NETFULL
            if (System.Web.HttpContext.Current != null)
            {
                // Make sure that the request is available in the current context.

                bool requestAvailable = false;

                try
                {
                    if (System.Web.Hosting.HostingEnvironment.IsHosted
                        && System.Web.HttpContext.Current != null
                        && System.Web.HttpContext.Current.Handler != null
                        && System.Web.HttpContext.Current.Request != null)
                    {
                        HttpRequest request = System.Web.HttpContext.Current.Request;

                        if (request != null)
                        {
                            requestAvailable = true;
                        }
                    }
                }
                catch
                {
                    // Web request may not be available at this time.
                    // Example: In the Application_Start method, there is an HttpContext, but no HttpContext.Request
                    //          System.Web.HttpException - Request is not available in this context
                    // Do nothing
                }

                // Attach the web request details

                if (requestAvailable)
                {
                    WebRequestDetail = new WebRequestDetail(this);

                    if (HttpContext.Current.User != null && HttpContext.Current.User.Identity != null)
                    {
                        UserName = HttpContext.Current.User.Identity.Name;
                    }
                }
            }

#elif NETSTANDARD1_3
            WebRequestDetail = new WebRequestDetail(this);
#endif

            // Fire event
            OnCaptureDetail?.Invoke(this);

            if (WebRequestDetail != null && WebRequestDetail.HttpMethod == null)
            {
                WebRequestDetail = null;
            }
        }

        public StackifyError SetAdditionalMessage(string message)
        {
            if (Error != null)
            {
                if (!String.IsNullOrEmpty(Error.Message))
                {
                    if (!String.IsNullOrEmpty(message) &&
                        !Error.Message.Equals(message, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Error.Message = Error.Message + " (" + message + ")";
                    }
                }
                else
                {
                    Error.Message = message;
                }
            }

            return this;
        }




        public static StackifyError New(Exception ex)
        {
            return new StackifyError(ex);
        }

        /// <summary>
        /// Errors we don't want to log as exceptions to our API
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static bool IgnoreError(StackifyError ex)
        {
            bool ignore = false;

            try
            {
                //if (ex._Exception is System.Threading.ThreadAbortException)
                //{
                //    ignore = true;
                //}
            }
            catch
            {
                // ignored
            }

            return ignore;
        }

        /// <summary>
        /// Errors we don't want to log as exceptions to our API
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static bool IgnoreError(Exception ex)
        {
            bool ignore = false;

            try
            {
                //if (ex is System.Threading.ThreadAbortException)
                //{
                //    ignore = true;
                //}
            }
            catch
            {
                // ignored
            }

            return ignore;
        }

        /// <summary>
        /// Used to send errors directly via our API and not through a logging framework
        /// </summary>
        /// <param name="apikey"></param>
        public void SendToStackify(string apikey = null)
        {
            try
            {
                //So it can't happen twice.
                if (!_Sent)
                {

                    if (this._Exception != null && IgnoreError(this._Exception))
                    {
                        StackifyAPILogger.Log("Not sending error because it is being ignored. Error Type: " + this._Exception.GetType());
                        return;
                    }
                    _Sent = true;

                    if (!Logger.ErrorShouldBeSent(this))
                    {
                        return;
                    }


                    if (!Logger.CanSend())
                    {
                        return;
                    }

                    LogMsg msg = null;

                    if (_InternalLogMsg != null)
                    {
                        msg = _InternalLogMsg;
                    }
                    else
                    {
                        msg = new LogMsg();
                    }
                    msg.Level = "ERROR";
                    msg.Ex = this;
                    msg.Msg = this.ToString();


                    Logger.QueueLogObject(msg, null);

                    _Sent = true;
                }
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log(e.ToString());
            }
        }

        public StackifyError SetTags(params string[] tags)
        {

            if (_InternalLogMsg == null)
                _InternalLogMsg = new LogMsg();

            if (_InternalLogMsg.Tags == null)
            {
                _InternalLogMsg.Tags = new List<string>();
            }

            foreach (var tag in tags)
            {
                if (tag != null)
                    _InternalLogMsg.Tags.Add(tag);
            }
            return this;
        }

        public StackifyError SetCustomProperties(object customProperties)
        {
            if (customProperties == null)
                return this;

            if (_InternalLogMsg == null)
                _InternalLogMsg = new LogMsg();

            _InternalLogMsg.data = HelperFunctions.SerializeDebugData(customProperties, true);

            return this;
        }

        public StackifyError SetUser(string userName)
        {
            UserName = userName;
            return this;
        }

        public StackifyError SetCustomer(string customerName)
        {
            CustomerName = customerName;
            return this;
        }

        public IEnumerable<TraceFrame> GetAllFrames()
        {
            return Error.GetAllFrames();
        }

        public override string ToString()
        {
            if (_Exception != null)
            {
                return _Exception.ToString();
            }
            else
            {
                return Error.ToString();
            }
        }
    }
}
