
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace StackifyLib.Models
{
    public class TraceFrame
    {
        [JsonProperty]
        public int? LineNum { get; set; }
        [JsonProperty]
        public string Method { get; set; }
        [JsonProperty]
        public string CodeFileName { get; set; }
    }
}
