using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace StackifyLib.Models
{
    public class LogMsgGroup
    {
        [JsonProperty]
        public int? CDID { get; set; }
        
        [JsonProperty]
        public int? CDAppID { get; set; }
        
        [JsonProperty]
        public Guid? AppNameID { get; set; }
        
        [JsonProperty]
        public Guid? AppEnvID { get; set; }
        
        [JsonProperty]
        public short? EnvID { get; set; }
        
        [JsonProperty]
        public string Env { get; set; }
        
        [JsonProperty]
        public string ServerName { get; set; }
        
        [JsonProperty]
        public string AppName { get; set; }
        
        [JsonProperty]
        public string AppLoc { get; set; }
        
        [JsonProperty]
        public string Logger { get; set; }

        [JsonProperty]
        public string Platform { get; set; }

        [JsonProperty]
        public List<LogMsg> Msgs { get; set; }

        public string GetUniqueKey()
        {
            return (Logger ?? string.Empty) + "" + ServerName + "-" + AppName + "-" + Env + "-" + (EnvID ?? 0) + "-" + (CDID ?? 0) + "-" + (CDAppID ?? 0);
        }
    }

    public class LogMsg
    {
        public LogMsg()
        {
            EpochMs = (long)DateTime.UtcNow.Subtract(Constants.Epoch).TotalMilliseconds;
            UploadErrors = 0;
        }

        [JsonIgnore]
        public int UploadErrors { get; set; }

        [JsonProperty]
        public string Msg { get; set; }
        
        [JsonProperty]
        public string data { get; set; } // serialized object as json
        
        [JsonProperty]
        public StackifyError Ex { get; set; }
        
        [JsonProperty]
        public string Th { get; set; } // thread
        
        [JsonProperty]
        public string ThOs { get; set; } // OS thread number
        
        [JsonProperty]
        public string TransID { get; set; } // transaction ID
        
        [JsonProperty]
        public long EpochMs { get; set; }
        
        [JsonProperty]
        public string Level { get; set; }

        [JsonProperty]
        public string UrlRoute { get; set; }

        [JsonProperty]
        public string UrlFull { get; set; }

        [JsonProperty]
        public string SrcMethod { get; set; }

        [JsonProperty]
        public int? SrcLine { get; set; }

        [JsonProperty]
        public string id { get; set; } // unique id

        [JsonProperty]
        public List<string> Tags { get; set; }

        [JsonProperty]
        public int Order { get; set; }

        [JsonIgnore]
        public LogMsgGroup AppDetails { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig | MethodImplOptions.NoOptimization)]
        public void SetLogMsgID(string id, int isError, string logLevel, string logMsg, string logData)
        {
            this.id = id;
        }
    }
}