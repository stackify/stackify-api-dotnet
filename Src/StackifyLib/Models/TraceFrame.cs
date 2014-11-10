
using System.Runtime.Serialization;

namespace StackifyLib.Models
{
    [DataContract]
    public class TraceFrame
    {
        [DataMember]
        public int? LineNum { get; set; }
        [DataMember]
        public string Method { get; set; }
        [DataMember]
        public string CodeFileName { get; set; }
    }
}
