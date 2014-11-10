using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    public class LogClient
    {
        private static bool _ServicePointSet = false;
        private LogQueue _LogQueue = null;
        private HttpClient _HttpClient = null;

        private readonly ErrorGovernor governor = new ErrorGovernor();

        private string _LoggerName = null;
        private string _ApiKey = null;
        private string _ApiUrl = null;

        public LogClient(string loggerName, string apikey = null, string apiurl = null)
        {

            StackifyAPILogger.Log("Creating new LogClient");

            _LoggerName = loggerName;
            _ApiKey = apikey;
            _ApiUrl = apiurl;

            _LogQueue = new LogQueue(this);

        }

        private void EnsureHttpClient()
        {
            if (_HttpClient == null)
            {
                _HttpClient = new HttpClient(_ApiKey, _ApiUrl);
            }
        }

        public string APIKey
        {
            get { return _ApiKey; }
        }

        public bool CanSend()
        {
            EnsureHttpClient();
            return _HttpClient.CanSend();
        }

        public AppIdentityInfo GetIdentity()
        {
            EnsureHttpClient();
            _HttpClient.IdentifyApp();
            return _HttpClient.AppIdentity;
        }

        public void Close()
        {
            try
            {
                if (_LogQueue != null)
                {
                    _LogQueue.Stop();
                }
            }
            catch
            {
            }

        }

        public void PauseUpload(bool isPaused)
        {
            _LogQueue.Pause(isPaused);
        }

        public bool ErrorShouldBeSent(StackifyError error)
        {
            return governor.ErrorShouldBeSent(error);
        }

        public void QueueMessage(LogMsg msg)
        {
            if (msg.id == null)
            {
                msg.SetLogMsgID(SequentialGuid.NewGuid().ToString(), msg.Ex != null);
            }

            _LogQueue.QueueLogMessage(msg);

        }
        
        internal Task<HttpClient.StackifyWebResponse> SendLogs(LogMsg[] messages)
        {
            try
            {
                StackifyAPILogger.Log("Trying to SendLogs");

                EnsureHttpClient();

                LogMsgGroup group = new LogMsgGroup();

                var identified = _HttpClient.IdentifyApp();


                if (_HttpClient.IsRecentError())
                {
                    var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                    tcs.SetException(new ApplicationException("Unable to send logs at this time due to recent error: " + (_HttpClient.LastErrorMessage ?? "")));
                    return tcs.Task;
                }

                if (!identified)
                {
                    var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                    tcs.SetException(new ApplicationException("Unable to send logs at this time. Unable to identify app"));
                    return tcs.Task;
                }


                group.Msgs = messages;


                //set these fields even if some could be null
                if (_HttpClient.AppIdentity != null)
                {
                    group.CDAppID = _HttpClient.AppIdentity.DeviceAppID;
                    group.CDID = _HttpClient.AppIdentity.DeviceID;
                    group.EnvID = _HttpClient.AppIdentity.EnvID;
                    group.Env = _HttpClient.AppIdentity.Env;
                    group.AppNameID = _HttpClient.AppIdentity.AppNameID;
                    group.AppEnvID = _HttpClient.AppIdentity.AppEnvID;
                    if (!String.IsNullOrWhiteSpace(_HttpClient.AppIdentity.DeviceAlias))
                    {
                        group.ServerName = _HttpClient.AppIdentity.DeviceAlias;
                    }

                    if (!String.IsNullOrWhiteSpace(_HttpClient.AppIdentity.AppName))
                    {
                        group.AppName = _HttpClient.AppIdentity.AppName;
                    }
                }

                var env = EnvironmentDetail.Get(false);

                //We use whatever the identity stuff says, otherwise we use the azure instance name and fall back to the machine name
                if (string.IsNullOrEmpty(group.ServerName))
                {
                    if (!string.IsNullOrEmpty(env.AzureInstanceName))
                    {
                        group.ServerName = env.AzureInstanceName;
                    }
                    else
                    {
                        group.ServerName = Environment.MachineName;
                    }
                }


                //if it wasn't set by the identity call above
                if (string.IsNullOrWhiteSpace(group.AppName))
                {
                    group.AppName = env.AppNameToUse();
                }
                else if (group.AppName.StartsWith("/LM/W3SVC"))
                {
                    group.AppName = env.AppNameToUse();
                }

                //only grab the location if we don't know what CD AppID
                if (group.CDAppID == null)
                {
                    group.AppLoc = env.AppLocation;
                }

                if (string.IsNullOrEmpty(group.Env))
                {
                    group.Env = env.ConfiguredEnvironmentName;
                }

                group.Logger = _LoggerName;
                group.Platform = ".net";

                //string jsonData = SimpleJson.SimpleJson.SerializeObject(group);

                string jsonData = JsonConvert.SerializeObject(group, new JsonSerializerSettings() {NullValueHandling = NullValueHandling.Ignore});

                string urlToUse = null;


                urlToUse = System.Web.VirtualPathUtility.AppendTrailingSlash(_HttpClient.BaseAPIUrl) + "Log/Save";


                if (!_ServicePointSet)
                {
                    ServicePointManager.FindServicePoint(urlToUse, null).ConnectionLimit = 10;
                    _ServicePointSet = true;
                }

                StackifyAPILogger.Log("Sending " + messages.Length.ToString() + " log messages");
                var task =
                    _HttpClient.SendAndGetResponseAsync(
                        urlToUse,
                        jsonData, jsonData.Length > 5000);

                return task;

            }
            catch (Exception ex)
            {
                Utils.StackifyAPILogger.Log(ex.ToString());

                var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                tcs.SetException(ex);
                return tcs.Task;
            }

            return null;
        }
    }
}
