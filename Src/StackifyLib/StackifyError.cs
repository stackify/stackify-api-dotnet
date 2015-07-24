using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using StackifyLib.Utils;


namespace StackifyLib
{
    using Models;
    using StackifyLib.Internal.Logs;
    using System.Threading;

    [DataContract]
    [JsonObject]
    public class StackifyError : ApplicationException
    {

        [DataMember]
        public long OccurredEpochMillis { get; set; }

        //our version of the error details
        [DataMember]
        public ErrorItem Error { get; set; }

        //Details of the device generating the error
        [DataMember]
        public EnvironmentDetail EnvironmentDetail { get; set; }

        [DataMember]
        public WebRequestDetail WebRequestDetail { get; set; }

        [DataMember]
        public Dictionary<string, string> ServerVariables { get; set; }

        [DataMember]
        public string CustomerName { get; set; }
        [DataMember]
        public string UserName { get; set; }

        public delegate void CapturDetailHandler(StackifyError ex);

        public static event CapturDetailHandler OnCaptureDetail;
        
        //internally kept reference to the exception
        [IgnoreDataMember]
        private Exception _Exception = null;
           
        [IgnoreDataMember]
        internal bool _Sent { get; set; }

        [IgnoreDataMember]
        private LogMsg _InternalLogMsg { get; set; }

        [IgnoreDataMember]
        public bool IsUnHandled { get; set; }


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
            :base(message)
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
                else if (exception is HttpUnhandledException && exception.InnerException != null)
                {
                    Error = new ErrorItem(exception.GetBaseException());
                    _Exception = exception.GetBaseException();
                }
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

            EnvironmentDetail = EnvironmentDetail.Get(false);
            ServerVariables = new Dictionary<string, string>();

            if (System.Web.HttpContext.Current != null)
            {
                // Make sure that the request is available in the current context.

                bool requestAvailable = false;

                try
                {
                    HttpRequest request = System.Web.HttpContext.Current.Request;

                    if (request != null)
                    {
                        requestAvailable = true;
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

            //Fire event
            if (OnCaptureDetail != null)
            {
                OnCaptureDetail(this);
            }
        }

        public StackifyError SetAdditionalMessage(string message)
        {
            if (Error != null)
            {
                if (!String.IsNullOrEmpty(Error.Message))
                {
                    if (!String.IsNullOrEmpty(message) &&
                        !Error.Message.Equals(message, StringComparison.InvariantCultureIgnoreCase))
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
                if (ex._Exception is System.Threading.ThreadAbortException)
                {
                    ignore = true;
                }
            }
            catch (Exception)
            {


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
                if (ex is System.Threading.ThreadAbortException)
                {
                    ignore = true;
                }
            }
            catch (Exception)
            {
                
                
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

            if(_InternalLogMsg == null)
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
