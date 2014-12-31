using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StackifyLib.Utils
{
    public class StackifyAPILogger
    {
        private static StringWriter _Logger = null;

        //must start with null so EvaluateLogEnabled() works below
        private static bool? _LogEnabled = null;

        public delegate void LogMessageHandler(string data);
        public static event LogMessageHandler OnLogMessage;

        static StackifyAPILogger()
        {
            EvaluateLogEnabled();
        }

        public static bool LogEnabled
        {
            get
            {
                EvaluateLogEnabled();
                return _LogEnabled ?? false;
            }
            set
            {
                _LogEnabled = value;
                Log("Logging enabled via code to value " + value.ToString());
            }
        }
            

        public static StringWriter Logger
        {
            set
            {
                _Logger = value;
            }
        }

        public static void Log(string message, bool logAnyways = false)
        {
            if (logAnyways || (_LogEnabled ?? false))
            {
                if (OnLogMessage != null)
                {
                    try
                    {
                        OnLogMessage("StackifyLib: " + message);
                    }
                    catch (Exception)
                    {
                        
                    }
                }

                if (_Logger != null)
                {
                    _Logger.Write("StackifyLib: " + message);
                }
                else
                {
                    Debug.WriteLine("StackifyLib: " + message);
                }
            }
            
        }

        private static void EvaluateLogEnabled()
        {
            if (_LogEnabled == null)
            {
				var setting = Config.Get("Stackify.ApiLog");

                if (setting != null && setting.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                {
                    _LogEnabled = true;

                    Log("StackifyLib: API Logger is enabled");
                }
                else
                {
                    _LogEnabled = false;
                    Log("StackifyLib: API Logger is disabled");
                }
            }
        }
    }
}
