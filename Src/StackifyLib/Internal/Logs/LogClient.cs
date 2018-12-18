using Newtonsoft.Json;
using StackifyLib.Models;
using StackifyLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace StackifyLib.Internal.Logs
{
    public class LogClient : ILogClient
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
            Config.LoadSettings();
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
            if (_LogQueue == null || !_LogQueue.CanQueue())
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



        private static long _lastEpochMs = 0;
        private static int _millisecondCount = 1;

        public void QueueMessage(LogMsg msg)
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


            //We need to do everything up to this point for sasquatch. Even if we aren't uploading the log.
            if (this.CanQueue())
            {
                _LogQueue.QueueLogMessage(msg);
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


        internal HttpClient.StackifyWebResponse SendLogsByGroups(LogMsg[] messages)
        {
            try
            {
                StackifyAPILogger.Log("Trying to SendLogs");

                EnsureHttpClient();


                var identified = _HttpClient.IdentifyApp();


                if (_HttpClient.IsRecentError())
                {
                    return new HttpClient.StackifyWebResponse() { Exception = new Exception("Unable to send logs at this time due to recent error: " + (_HttpClient.LastErrorMessage ?? "")) };
                }

                if (!identified)
                {
                    return new HttpClient.StackifyWebResponse() { Exception = new Exception("Unable to send logs at this time. Unable to identify app") };
                }

                var groups = SplitLogsToGroups(messages);

                var settings = new JsonSerializerSettings() { MaxDepth = Config.LoggingJsonMaxDepth, NullValueHandling = NullValueHandling.Ignore };

                string jsonData;
                using (var writer = new StringWriter())
                {
                    using (var jsonWriter = new MaxDepthJsonTextWriter(writer, settings))
                    {
                        JsonSerializer.CreateDefault().Serialize(jsonWriter, groups);
                        jsonData = writer.ToString();
                    }
                }

                string urlToUse = (_HttpClient.BaseAPIUrl) + "Log/SaveMultipleGroups";


                if (!_ServicePointSet)
                {
#if NETFULL
                    ServicePointManager.FindServicePoint(urlToUse, null).ConnectionLimit = 10;
#endif
                    _ServicePointSet = true;
                }

                StackifyAPILogger.Log("Sending " + messages.Length.ToString() + " log messages via send multi groups");
                var response =
                    _HttpClient.SendJsonAndGetResponse(
                        urlToUse,
                        jsonData, jsonData.Length > 5000);

                messages = null;
                groups = null;

                return response;

            }
            catch (Exception ex)
            {
                Utils.StackifyAPILogger.Log(ex.ToString());

                return new HttpClient.StackifyWebResponse() { Exception = ex };
            }
        }
    }
}
