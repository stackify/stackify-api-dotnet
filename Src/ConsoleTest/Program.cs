using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                log.Info("test info 2");
                log.Debug("test debug 2");
                log.Fatal("test fatal 2");
                log.Error("test error 2");
            }
            Debug.WriteLine("Closing app...");
        }


    }
}
