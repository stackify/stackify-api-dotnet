using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace StackifyLib.Models
{
    public class ErrorItem
    {
        public ErrorItem()
        {

        }

        public ErrorItem(Exception ex)
        {

            try
            {
                var keys = ex.Data.Keys;
                Data = new Dictionary<string, string>();

                foreach (var item in keys)
                {
                    if (ex.Data[item] != null)
                    {
                        Data.Add(item.ToString(), ex.Data[item].ToString());
                    }
                }

                Message = ex.Message;
#if NETFULL
                if (ex is System.Data.SqlClient.SqlException)
                {
                    System.Data.SqlClient.SqlException sql = ex as System.Data.SqlClient.SqlException;

                    if (sql.Errors.Count > 0 && sql.Errors[0].Number != 0)
                    {
                        ErrorTypeCode = sql.Errors[0].Number.ToString();
                        if (!string.IsNullOrEmpty(sql.Errors[0].Server))
                        {
                            Message = Message + "\r\nServer: " + sql.Errors[0].Server;
                        }
                    }
                    else
                    {
                        ErrorTypeCode = Marshal.GetHRForException(ex).ToString();
                    }
                }
                else
                {
                    ErrorTypeCode = Marshal.GetHRForException(ex).ToString();
                }
#endif

                //this is the default HResult. Ignore it?
                //Leave it since we are already using it. Would cause unique errors to reset on people
                //if (!string.IsNullOrEmpty(ErrorTypeCode) && ErrorTypeCode == "-2146233088")
                //{
                //    ErrorTypeCode = "";
                //}

                var t = ex.GetType();


                ErrorType = t.FullName;

                if (ex is StringException)
                {
                    AddTraceFrames((StringException)ex);
                }
                else
                {
                    AddTraceFrames(ex);
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            if (ex.InnerException != null)
            {
                InnerError = new ErrorItem(ex.InnerException);
            }
        }

        [JsonProperty]
        public ErrorItem InnerError { get; set; }
        [JsonProperty]
        public List<TraceFrame> StackTrace { get; set; }
        [JsonProperty]
        public string Message { get; set; }
        [JsonProperty]
        public string ErrorType { get; set; }
        [JsonProperty]
        public string ErrorTypeCode { get; set; }
        [JsonProperty]
        public Dictionary<string, string> Data { get; set; }
        [JsonProperty]
        public string SourceMethod { get; set; }

        private void AddTraceFrames(StringException ex)
        {
            StackTrace = ex.TraceFrames;

            if (StackTrace != null && StackTrace.Count > 0)
            {
                this.SourceMethod = ex.TraceFrames[0].Method;
            }
        }

        private void AddTraceFrames(Exception ex)
        {
            this.StackTrace = new List<TraceFrame>();
            var stackTrace = new StackTrace(ex, true);

            var errorframes = stackTrace.GetFrames();

            string lastErrorFrameMethodName = null;

            if (errorframes != null)
            {
                foreach (StackFrame frame in errorframes)
                {
                    MethodBase method = frame.GetMethod();

                    var fullName = GetMethodFullName(method);

                    bool isSource = false;//(ex.TargetSite != null && ex.TargetSite == method);

                    if (isSource)
                    {
                        this.SourceMethod = fullName;
                    }

                    StackTrace.Add(new TraceFrame()
                    {
                        CodeFileName = frame.GetFileName(),
                        LineNum = frame.GetFileLineNumber(),
                        Method = fullName
                    });

                    lastErrorFrameMethodName = fullName;
                }
            }

#if NETFULL
            var stackTrace2 = new StackTrace(true);
            var allFrames = stackTrace2.GetFrames();


            //logic to add missing frames not showing up in the normal exception stack trace some times
            if (allFrames != null && (lastErrorFrameMethodName != null || this.SourceMethod == null))
            {
                bool foundLast = false;

                foreach (StackFrame frame in allFrames)
                {
                    MethodBase method = frame.GetMethod();

                    var fullName = GetMethodFullName(method);

                    if (!foundLast)
                    {
                        if (lastErrorFrameMethodName == fullName)
                        {
                            foundLast = true;
                            continue;
                        }
                    }


                    if (foundLast)
                    {
                        StackTrace.Add(new TraceFrame()
                        {
                            // LibraryName = method.Module.Name,
                            CodeFileName = frame.GetFileName(),
                            LineNum = frame.GetFileLineNumber(),
                            Method = GetMethodFullName(method)
                        });
                    }
                }
            }
#endif


        }

        public static string GetMethodFullName(MethodBase method, bool simpleMethodNames = false)
        {
            if (method == null)
                return "Unknown";

#if NETFULL
            if (method.ReflectedType != null)
            {
                if (simpleMethodNames)
                {
                    return method.ReflectedType.FullName + "." + method.Name;
                }

                try
                {
                    string fullName = method.ReflectedType.FullName + "." +
                                           typeof(MethodBase).InvokeMember("FormatNameAndSig",
                                                                            BindingFlags.NonPublic | BindingFlags.InvokeMethod |
                                                                            BindingFlags.Instance, null, method, null);

                    if (fullName != null)
                    {
                        return fullName;
                    }
                }
                catch (Exception)
                {
                }
            }
#endif

            StringBuilder sb = new StringBuilder();

            if (method.DeclaringType != null)
            {
                sb.Append(method.DeclaringType.FullName);
                sb.Append(".");
            }

            sb.Append(method.Name);
            sb.Append("(");

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; ++i)
                {
                    if (0 < i)
                    {
                        sb.Append(", ");
                    }

                    ParameterInfo p = parameters[i];

                    if (p != null)
                    {
                        if (p.ParameterType != null)
                        {
                            sb.Append(p.ParameterType.FullName);
                        }
                        else
                        {
                            sb.Append(p.ToString());
                        }
                    }
                }
            }

            sb.Append(")");

            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}: {1}", this.ErrorType, this.Message);

            ErrorItem innerError = this.InnerError;

            while (innerError != null)
            {
                sb.AppendFormat(" ---> {0}: {1}", innerError.ErrorType, innerError.Message);
                innerError = innerError.InnerError;
            }
            sb.Append("\r\n" + this.FramesToString());
            return sb.ToString();
        }

        public IEnumerable<TraceFrame> GetAllFrames()
        {
            foreach (var item in this.StackTrace)
                yield return item;

            if (InnerError != null)
            {
                foreach (var item in InnerError.GetAllFrames())
                    yield return item;
            }
        }


        public string FramesToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in this.StackTrace)
            {
                sb.AppendFormat("  at {0}\r\n", item.Method);
            }

            if (InnerError != null)
            {
                sb.Append("--- End of inner exception stack trace ---\r\n");
                sb.Append(InnerError.FramesToString());
            }

            return sb.ToString();
        }
    }
}
