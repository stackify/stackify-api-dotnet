using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
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

            StackifyAPILogger.Log("Creating new LogClient " + loggerName);

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

        public string LoggerName
        {
            get { return _LoggerName; }
        }

        public string APIKey
        {
            get
            {
                EnsureHttpClient();
                return _HttpClient.APIKey;
            }
        }

        [Obsolete("Use CanQueue instead")]
        public bool CanSend()
        {
            return CanQueue();
        }

        public bool CanQueue()
        {
            if (!_LogQueue.CanQueue())
            {
                return false;
            }

            EnsureHttpClient();
            return _HttpClient.IsAuthorized();
        }

        public bool IsAuthorized()
        {
            EnsureHttpClient();
            return _HttpClient.IsAuthorized();
           
        }

        public bool CanUpload()
        {
            EnsureHttpClient();
            return _HttpClient.CanUpload();
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
                    //stop will flush the queue
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
                int isError = 0;

                if (msg.Ex != null)
                {
                    isError = 1;
                }
                msg.SetLogMsgID(SequentialGuid.NewGuid().ToString(), isError, msg.Level);
            }

            _LogQueue.QueueLogMessage(msg);

        }

        private List<Models.LogMsgGroup> SplitLogsToGroups(LogMsg[] messages)
        {
            Dictionary<string, Models.LogMsgGroup> groups = new Dictionary<string, LogMsgGroup>();

            foreach (var message in messages)
            {
                string groupKey = "default";

                if (message.AppDetails != null)
                {
                    groupKey = message.AppDetails.GetUniqueKey();
                }

                if (!groups.ContainsKey(groupKey))
                {
                    if (groupKey == "default" || message.AppDetails == null)
                    {
                        groups["default"] = CreateDefaultMsgGroup();
                    }
                    else
                    {
                        var d = message.AppDetails;
                        var group = new LogMsgGroup()
                        {
                            AppEnvID = d.AppEnvID,
                            AppLoc = d.AppLoc,
                            AppName =  d.AppName,
                            AppNameID = d.AppNameID,
                            CDAppID = d.CDAppID,
                            CDID = d.CDID,
                            Env = d.Env,
                            EnvID = d.EnvID,
                            Logger = _LoggerName,
                            Platform = ".net",
                             ServerName = d.ServerName,
                             Msgs = new List<LogMsg>()
                        };
                        
                        groups[groupKey] = group;
                    }
                }

                groups[groupKey].Msgs.Add(message);
            }
            return groups.Values.ToList();
        }

        private Models.LogMsgGroup CreateDefaultMsgGroup()
        {
            Models.LogMsgGroup group = new LogMsgGroup();
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

            group.AppLoc = env.AppLocation;

            if (string.IsNullOrEmpty(group.Env))
            {
                group.Env = env.ConfiguredEnvironmentName;
            }

            group.Logger = _LoggerName;
            group.Platform = ".net";
            group.Msgs = new List<LogMsg>();
            return group;
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
                    tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = new ApplicationException("Unable to send logs at this time due to recent error: " + (_HttpClient.LastErrorMessage ?? "")) });
                    return tcs.Task;
                }

                if (!identified)
                {
                    var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                    tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = new ApplicationException("Unable to send logs at this time. Unable to identify app") });
                    return tcs.Task;
                }


                group.Msgs = messages.ToList();


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

                group.AppLoc = env.AppLocation;

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
                    _HttpClient.SendJsonAndGetResponseAsync(
                        urlToUse,
                        jsonData, jsonData.Length > 5000);

                return task;

            }
            catch (Exception ex)
            {
                Utils.StackifyAPILogger.Log(ex.ToString());

                var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = ex });
//                tcs.SetException(ex);
                return tcs.Task;
            }

            return null;
        }

        internal Task<HttpClient.StackifyWebResponse> SendLogsByGroups(LogMsg[] messages)
        {
            try
            {
                StackifyAPILogger.Log("Trying to SendLogs");

                EnsureHttpClient();


                var identified = _HttpClient.IdentifyApp();


                if (_HttpClient.IsRecentError())
                {
                    var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                    tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = new ApplicationException("Unable to send logs at this time due to recent error: " + (_HttpClient.LastErrorMessage ?? "")) });
                    return tcs.Task;
                }

                if (!identified)
                {
                    var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                    tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = new ApplicationException("Unable to send logs at this time. Unable to identify app") });
                    return tcs.Task;
                }

                var groups = SplitLogsToGroups(messages);

                string jsonData = JsonConvert.SerializeObject(groups, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

                
                string urlToUse = System.Web.VirtualPathUtility.AppendTrailingSlash(_HttpClient.BaseAPIUrl) + "Log/SaveMultipleGroups";


                if (!_ServicePointSet)
                {
                    ServicePointManager.FindServicePoint(urlToUse, null).ConnectionLimit = 10;
                    _ServicePointSet = true;
                }

                StackifyAPILogger.Log("Sending " + messages.Length.ToString() + " log messages via send multi groups");
                var task =
                    _HttpClient.SendJsonAndGetResponseAsync(
                        urlToUse,
                        jsonData, jsonData.Length > 5000);


                messages = null;
                groups = null;

                return task;

            }
            catch (Exception ex)
            {
                Utils.StackifyAPILogger.Log(ex.ToString());

                var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = ex });
                return tcs.Task;
            }

            return null;
        }
    }
}
