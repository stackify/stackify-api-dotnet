using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Models;
using StackifyLib.Utils;
using System.Net;

namespace StackifyLib.Internal.Logs
{
    internal class LogClient : ILogClient
    {
        private IScheduledLogHandler _logHandler;
        private readonly IErrorGovernor _governor;
        private string _loggerName;
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
            catch
            {
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
    }
}
