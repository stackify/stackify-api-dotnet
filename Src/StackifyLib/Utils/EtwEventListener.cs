#if NETFULL
#if !NET40
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StackifyLib.Utils
{
    public class EtwEventListener : EventListener
    {
        private static readonly Guid tplGuid = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
        //  private static readonly Guid aspnetGuid = new Guid("aff081fe-0247-4275-9c4e-021f3dc1da35");

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Guid == tplGuid)
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
         
        }
    }
}
#endif
#endif