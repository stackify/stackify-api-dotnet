#if NETFULL
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace StackifyLib.Utils
{
    public class AsyncTracer
    {
        private static bool _AsyncTracerEnabled = false;
        private static DateTime _LastEnabledTest = DateTime.MinValue;

        public static bool EnsureAsyncTracer()
        {
            if (_AsyncTracerEnabled)
                return _AsyncTracerEnabled;

            if (_LastEnabledTest > DateTime.UtcNow.AddMinutes(-5))
            {
                return _AsyncTracerEnabled;
            }

            try
            {
                var t = Type.GetType("System.Threading.Tasks.AsyncCausalityTracer");
                if (t != null)
                {
                    var field = t.GetField("f_LoggingOn", BindingFlags.NonPublic | BindingFlags.Static);

                    if (field == null)
                    {
                        StackifyLib.Utils.StackifyAPILogger.Log("Unable to enable the AsyncCausalityTracer, f_LoggingOn field not found");
                        return _AsyncTracerEnabled;
                    }
                

                    if (field.FieldType.Name == "Boolean")
                    {
                        bool current = (bool)field.GetValue(null);

                        if (!current)
                        {
                            field.SetValue(null, true);
                        }
                    }
                    else
                    {
                        field.SetValue(null, (byte)4); 
                    }

                    _AsyncTracerEnabled = true;
                    


                }
                else
                {
                    _AsyncTracerEnabled = true; //never going to work.
                    StackifyLib.Utils.StackifyAPILogger.Log("Unable to enable the AsyncCausalityTracer, class not found");
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("EnsureAsyncTracer Exception: " + ex.ToString());
                Debug.WriteLine(ex);
            }
            _LastEnabledTest = DateTime.UtcNow;

            return _AsyncTracerEnabled;
        }
    }
}
#endif