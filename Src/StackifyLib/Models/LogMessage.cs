using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace StackifyLib.Models
{

    /// <summary>
    /// Helper class for logging a message and object both
    /// </summary>
    [Serializable]
    public class LogMessage
    {
        public object json { get; set; }
        public string message { get; set; }

        public override string ToString()
        {
            if (json != null)
            {
                return message + " " + JsonConvert.SerializeObject(json, new JsonSerializerSettings{ReferenceLoopHandling = ReferenceLoopHandling.Serialize});
            }
            else
            {
                return message;
            }
        }
    }
}
