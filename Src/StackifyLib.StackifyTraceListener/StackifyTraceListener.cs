using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace StackifyLib
{
    public class StackifyTraceListener : TraceListener
    {
        public override void WriteLine(object o, string category)
        {
            base.WriteLine(o, category);
        }

        public override void WriteLine(string message, string category)
        {
            base.WriteLine(message, category);
        }

        public override void WriteLine(object o)
        {
            base.WriteLine(o);
        }

        public override void Write(string message)
        {
            //ignore this
        }

        public override void WriteLine(string message)
        {

        }

        public override void Fail(string message)
        {
            base.Fail(message);
        }

        public override void Fail(string message, string detailMessage)
        {
            base.Fail(message, detailMessage);
        }

        public override void Flush()
        {
            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
