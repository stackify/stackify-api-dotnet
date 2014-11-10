using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StackifyLib.Models
{

    /// <summary>
    /// Helper class for logging a message and object both
    /// </summary>
    public class LogMessage
    {
        public object json { get; set; }
        public string message { get; set; }

        public override string ToString()
        {
            return message;
        }
    }
}
