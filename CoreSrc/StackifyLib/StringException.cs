using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using StackifyLib.Models;

namespace StackifyLib
{
    /// <summary>
    /// Used when someone logs an error but only logs a string with no exception
    /// </summary>
    public class StringException : Exception
    {
        public StringException(string message)
            : base(message)
        {

        }

        //Used to hold the stack trace for the exception and we have to manually figure it out
        public List<TraceFrame> TraceFrames { get; set; }
    }
}

/*
 * Example usage from log4net appender
 *
                StringException stringEx = new StringException(loggingEvent.RenderedMessage);
                stringEx.TraceFrames = new List<TraceFrame>();


                stringEx.TraceFrames = StackifyLib.Logger.GetCurrentStackTrace(loggingEvent.LoggerName);

                if (stringEx.TraceFrames.Any())
                {
                    var first = stringEx.TraceFrames.First();

                    msg.SrcMethod = first.Method;
                    msg.SrcLine = first.LineNum;
                }

                //Make error out of log message
                error = StackifyError.New(stringEx);


*/