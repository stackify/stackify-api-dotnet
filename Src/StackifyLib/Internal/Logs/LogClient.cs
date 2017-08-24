using Newtonsoft.Json;
using StackifyLib.Models;
using StackifyLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Logs
{
    internal class LogClient : ILogClient
    {
        private readonly IScheduledLogHandler _logHandler;
        private readonly IErrorGovernor _governor;
        private readonly string _loggerName;
        private static long _lastEpochMs = 0;
        private static int _millisecondCount = 1;

        public LogClient(
            IScheduledLogHandler logHandler,
            IErrorGovernor governor,
            string loggerName)
        {
            Config.LoadSettings();

            StackifyAPILogger.Log("Creating new LogClient " + loggerName);

            _logHandler = logHandler;
            _governor = governor;
            _loggerName = loggerName;
        }

        public bool CanQueue()
        {
            if (_logHandler == null || _logHandler.CanQueue() == false)
            {
                return false;
            }
            return true;
        }

        public void Close()
        {
            try
            {
                _logHandler?.Stop().Wait();
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log($"An error ocurred while closing the log handler.\n{e}");
            }
        }

        public void PauseUpload(bool isPaused)
        {
            _logHandler.Pause(isPaused);
        }

        public bool ErrorShouldBeSent(StackifyError error)
        {
            return _governor.ErrorShouldBeSent(error);
        }

        public void QueueMessage(LogMsg msg)
        {
            msg.Logger = _loggerName;
            var claims = AppClaimsManager.Get();
            QueueMessage(msg, claims);
        }

        public void QueueMessage(LogMsg msg, AppClaims appClaims)
        {
            if (msg == null) return;

            int isError = 0;

            if (msg.id == null) //should be null unless someone is using our API directly and setting it
            {
                msg.id = SequentialGuid.NewGuid().ToString();
            }

            if (msg.Ex != null)
            {
                isError = 1;
            }

            // works on the assumption that the epochMS will always be incrementing as it reaches this point
            if (_lastEpochMs < msg.EpochMs)
            {
                // reset counter if we are no longer in the same ms
                //https://msdn.microsoft.com/en-us/library/system.threading.interlocked_methods(v=vs.110).aspx
                Interlocked.Exchange(ref _lastEpochMs, msg.EpochMs);
                Interlocked.Exchange(ref _millisecondCount, 1);
                msg.Order = 1;
            }
            else if (_lastEpochMs == msg.EpochMs)
            {
                msg.Order = Interlocked.Increment(ref _millisecondCount);
            }
            // else defaulted to 0

            //Used by Stackify profiler only
            if (Logger.PrefixEnabled())
            {
                msg.SetLogMsgID(msg.id, isError, msg.Level, msg.Msg, msg.data);
            }
            else
            {
                msg.SetLogMsgID(msg.id, isError, msg.Level, null, null);
            }

            // We need to do everything up to this point for sasquatch. Even if we aren't uploading the log.
            if (this.CanQueue())
            {
                _logHandler.QueueLogMessage(appClaims, msg);
            }
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
                        var defaults = CreateDefaultMsgGroup();

                        //default app, env, and server name if not set to whatever the current one is
                        //do not default the other fields as they not match what is being set. 
                        //i.e. the default appnameid is not the correct id for a new custom app name being used.
                        var d = message.AppDetails;
                        var group = new LogMsgGroup()
                        {
                            AppEnvID = d.AppEnvID,
                            AppLoc = d.AppLoc,
                            AppName = d.AppName ?? defaults.AppName,
                            AppNameID = d.AppNameID,
                            CDAppID = d.CDAppID,
                            CDID = d.CDID,
                            Env = d.Env ?? defaults.Env,
                            EnvID = d.EnvID,
                            Logger = d.Logger ?? _LoggerName,
                            Platform = ".net",
                            ServerName = d.ServerName ?? defaults.ServerName,
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
                    group.ServerName = env.DeviceName;
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

            //override it
            if (!string.IsNullOrEmpty(env.ConfiguredEnvironmentName))
            {
                group.Env = env.ConfiguredEnvironmentName;
            }

            group.Logger = _LoggerName;
            group.Platform = ".net";
            group.Msgs = new List<LogMsg>();
            return group;
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
                    tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = new Exception("Unable to send logs at this time due to recent error: " + (_HttpClient.LastErrorMessage ?? "")) });
                    return tcs.Task;
                }

                if (!identified)
                {
                    var tcs = new TaskCompletionSource<HttpClient.StackifyWebResponse>();
                    tcs.SetResult(new HttpClient.StackifyWebResponse() { Exception = new Exception("Unable to send logs at this time. Unable to identify app") });
                    return tcs.Task;
                }

                var groups = SplitLogsToGroups(messages);

                string jsonData = JsonConvert.SerializeObject(groups, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

                
                string urlToUse = (_HttpClient.BaseAPIUrl) + "Log/SaveMultipleGroups";


                if (!_ServicePointSet)
                {
#if NET451 || NET45 || NET40
                    ServicePointManager.FindServicePoint(urlToUse, null).ConnectionLimit = 10;
#endif
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
