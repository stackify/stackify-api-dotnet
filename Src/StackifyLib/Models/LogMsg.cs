using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using StackifyLib.Utils;

namespace StackifyLib.Models
{
    public class LogMsg
    {

        public LogMsg()
        {
            EpochMs = (long)HighPrecisionTime.UtcNow.Subtract(StackifyConstants.Epoch).TotalMilliseconds;
            UploadErrors = 0;
        }

        public string Msg { get; set; }        
        public string data { get; set; } //serialized object as json
        public StackifyError Ex { get; set; }        
        public string Th { get; set; } //thread        
        public string ThOs { get; set; } //OS thread number        
        public string TransID { get; set; } //transaction ID        
        public long EpochMs { get; set; }        
        public string Level { get; set; }
        public string UrlRoute { get; set; }
        public string UrlFull { get; set; }
        public string SrcMethod { get; set; }
        public int? SrcLine { get; set; }
        public string id { get; set; } //unique id
        public List<string> Tags { get; set; }
        public int Order { get; set; }
        public string Logger { get; set; }
        
        [JsonIgnore]
        public LogMsgGroup AppDetails { get; set; }

        [JsonIgnore]
        public int UploadErrors { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig | MethodImplOptions.NoOptimization)]
        public void SetLogMsgID(string id, int isError, string logLevel, string logMsg, string logData)
        {
            this.id = id;
        }
    }
}
