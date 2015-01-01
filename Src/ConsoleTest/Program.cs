using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackifyLib;

namespace ConsoleTest
{
    class Program
    {
        static log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            StackifyLib.Utils.StackifyAPILogger.LogEnabled = true;
            
            for (int i = 0; i < 1000; i++)
            {
				var ex = new ApplicationException("Test error");
                Logger.QueueException(ex);
                Logger.Queue("DEBUG", "test " + i);
				//log.Info("test info " + i);
				//log.Info("object test" + i, new { First = 1, Second = 2, OK = true, DT = DateTime.UtcNow });
				//log.Info(new { OjectOnly = true, DT = DateTime.UtcNow, Child = new { First = 1, Second = 2, OK = true, DT = DateTime.UtcNow } });
				//log.Debug("test debug " + i);
				//log.Fatal("test fatal " + i);
				//log.Error("test error " + i);
				//log.Error("test error object" + i, ex);
                System.Threading.Thread.Sleep(2000);
            }
            Debug.WriteLine("Closing app...");
        }

        static void StackifyAPILogger_OnLogMessage(string data)
        {
            Debug.WriteLine(data);
        }


    }
}
