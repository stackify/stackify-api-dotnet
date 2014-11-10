using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using StackifyLib.Models;
using log4net.Core;
using log4net;
using StackifyLib;

namespace StackifyLib
{
    public static class Extensions
    {
        private static LogMessage GetMessage(string message, object debugData)
        {
            return new LogMessage() { message = message, json = debugData };
        }

        public static void Debug(this ILog log, string message, object debugData)
        {
            log.Debug(GetMessage(message, debugData));
        }

        public static void Info(this ILog log, string message, object debugData)
        {
            log.Info(GetMessage(message, debugData));
        }

        public static void Warn(this ILog log, string message, object debugData)
        {
            log.Warn(GetMessage(message, debugData));
        }

        public static void Warn(this ILog log, string message, object debugData, Exception exception)
        {
            log.Warn(GetMessage(message, debugData), exception);
        }

        public static void Error(this ILog log, string message, object debugData)
        {
            log.Error(GetMessage(message, debugData));
        }

        public static void Error(this ILog log, string message, object debugData, Exception exception)
        {
            log.Error(GetMessage(message, debugData), exception);
        }

        public static void Fatal(this ILog log, string message, object debugData)
        {
            log.Fatal(GetMessage(message, debugData));
        }

        public static void Fatal(this ILog log, string message, object debugData, Exception exception)
        {
            log.Fatal(GetMessage(message, debugData), exception);
        }
    }
}
