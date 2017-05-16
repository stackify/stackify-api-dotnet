using System.Collections.Generic;

namespace StackifyLib.Models
{
    public class LogMsgGroup : Identifiable
    {
        public List<LogMsg> Msgs { get; set; }
    }
}
