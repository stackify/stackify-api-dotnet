using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace StackifyLib.Models
{
    [DataContract]
    public class LogMsgGroup
    {
        [DataMember]
        public int? CDID { get; set; }
        [DataMember]
        public int? CDAppID { get; set; }
        [DataMember]
        public Guid? AppNameID { get; set; }
        [DataMember]
        public Guid? AppEnvID { get; set; }
        [DataMember]
        public short? EnvID { get; set; }
        [DataMember]
        public string Env { get; set; }
        [DataMember]
        public string ServerName { get; set; }
        [DataMember]
        public string AppName { get; set; }
        [DataMember]
        public string AppLoc { get; set; }
        [DataMember]
        public string Logger { get; set; }

        [DataMember]
        public string Platform { get; set; }
    
        [DataMember]
        public List<LogMsg> Msgs { get; set; }

        public string GetUniqueKey()
        {
            return ServerName + "-" + AppName + "-" + Env + "-" + (EnvID ?? 0) + "-" + (CDID ?? 0) + "-" +
                   (CDAppID ?? 0);
        }

    }

    [DataContract]
    public class LogMsg
    {
        private static DateTime _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        public LogMsg()
        {
            EpochMs = (long)DateTime.UtcNow.Subtract(_Epoch).TotalMilliseconds;
            UploadErrors = 0;
        }

        [IgnoreDataMember]
        public int UploadErrors { get; set; }

        [DataMember]
        public string Msg { get; set; }
        [DataMember]
        public string data { get; set; } //serialized object as json
        [DataMember]
        public StackifyError Ex { get; set; }
        [DataMember]
        public string Th { get; set; } //thread
        [DataMember]
        public string ThOs { get; set; } //OS thread number
        [DataMember]
        public string TransID { get; set; } //transaction ID
        [DataMember]
        public long EpochMs { get; set; }
        [DataMember]
        public string Level { get; set; }

        [DataMember]
        public string UrlRoute { get; set; }


        [DataMember]
        public string UrlFull { get; set; }


        [DataMember]
        public string SrcMethod { get; set; }

        [DataMember]
        public int? SrcLine { get; set; }

        [DataMember]
        public string id { get; set; } //unique id

        [DataMember]
        public List<string> Tags { get; set; }

        [IgnoreDataMember]
        public LogMsgGroup AppDetails { get; set; }


        [MethodImpl(MethodImplOptions.NoInlining |  MethodImplOptions.PreserveSig | MethodImplOptions.NoOptimization)]
        public void SetLogMsgID(string id, int isError, string logLevel)
        {
            this.id = id;
        }
    }
}
