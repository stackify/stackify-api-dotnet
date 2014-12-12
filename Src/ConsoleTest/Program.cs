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
                Logger.QueueException(new ApplicationException("Test error"));
                Logger.Queue("DEBUG", "test " + i);
                //log.Info("test info " + i);
                //log.Debug("test debug " + i);
                //log.Fatal("test fatal " + i);
                //log.Error("test error "  + i);
                System.Threading.Thread.Sleep(1000);
            }
            Debug.WriteLine("Closing app...");
        }

        static void StackifyAPILogger_OnLogMessage(string data)
        {
            Debug.WriteLine(data);
        }


    }
}
