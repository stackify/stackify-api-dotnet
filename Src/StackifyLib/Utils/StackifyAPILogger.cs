using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StackifyLib.Utils
{
    public class StackifyAPILogger
    {
 #if NETSTANDARD1_3
        private static readonly string DefaultLoggerPathAndFile = Path.Combine(System.AppContext.BaseDirectory, "stackify.debug.log");
#elif NETFULL
        private static readonly string DefaultLoggerPathAndFile = Path.Combine(Environment.CurrentDirectory, "stackify.debug.log");
#endif
        private static readonly object LoggerDefaultLock = new object();
        private static StreamWriter _loggerDefault;

        private static StringWriter _logger;
        private static bool? _logEnabled;

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
                var enabled = _logEnabled ?? false;

                return enabled;
            }
            set
            {
                _logEnabled = value;
            }
        }

        public static StringWriter Logger
        {
            set
            {
                _logger = value;
            }
        }



        public static void Log(string message, bool logAnyways = false)
        {
            try
            {
                if (logAnyways || (_logEnabled ?? false))
                {
                    var msg = $"{DateTime.UtcNow:yyyy/MM/dd HH:mm:ss,fff}/GMT StackifyLib: {message}";

                    OnLogMessage?.Invoke(msg);

                    if (_logger != null)
                    {
                        _logger.Write(msg);
                    }
                    else
                    {
                        Debug.WriteLine(msg);

                        lock (LoggerDefaultLock)
                        {
                            if (_loggerDefault == null)
                            {
                                _loggerDefault = new StreamWriter(new FileStream(DefaultLoggerPathAndFile, FileMode.Append, FileAccess.Write, FileShare.Read));
                            }
                        }

                        _loggerDefault.WriteLine(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"#StackifyAPILogger #Log failed\r\n{ex}");
            }
        }

        public static void Log(string message, Exception ex, bool logAnyways = false)
        {
            try
            {
                if (logAnyways || (_logEnabled ?? false))
                {
                    var msg = $"{message}\r\n{ex}";

                    Log(msg, logAnyways);
                }
            }
            catch
            {
                Debug.WriteLine($"#StackifyAPILogger #Log failed\r\n{ex}");
            }
        }

        private static void EvaluateLogEnabled()
        {
            if (_logEnabled == null)
            {
                var setting = Config.Get("Stackify.ApiLog");

                if (setting != null && setting.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase))
                {
                    _logEnabled = true;

                    Log("API Logger is enabled");
                }
                else
                {
                    _logEnabled = false;
                    Log("API Logger is disabled");
                }
            }
        }
    }
}
