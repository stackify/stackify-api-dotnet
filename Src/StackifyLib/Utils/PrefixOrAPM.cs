using System;
using System.Collections.Generic;
//using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace StackifyLib.Utils
{

    /*
     * Used to identify if the current app is running on the same computer as Stackify Prefix or APM
     */ 

    internal class PrefixOrAPM
    {
        internal enum ProfilerType
        {
            None,
            Prefix,
            APM
        }

        static DateTime _LastCheck = DateTime.MinValue;
        static ProfilerType _LastProfilerType = ProfilerType.None;
        private static bool _ScanProcessException = false;
        internal static ProfilerType GetProfilerType()
        {
            if (_LastCheck > DateTime.UtcNow.AddMinutes(-1))
            {
                return _LastProfilerType;
            }
            _LastCheck = DateTime.UtcNow;
            bool foundProcess = false;

            string instanceID = Left(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"), 6);
            string siteName = (Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME") ?? "").TrimStart('~', '1');

            //is azure app service?
            if (!string.IsNullOrEmpty(instanceID) && !string.IsNullOrEmpty(siteName))
            {
                _LastProfilerType = ProfilerType.APM;
                return _LastProfilerType;
            }

            if (!_ScanProcessException)
            {
                try
                {
                    foreach (var process in Process.GetProcesses().ToList())
                    {
                        if (process.Id <= 0)
                            continue;

                        try
                        {
                            switch (process?.ProcessName?.ToLower().Replace(".vshost", ""))
                            {
                                case "devdashservice":
                                case "stackifytracerservice":
                                case "stackifytracernotifier":
                                case "devdashtestconsole":
                                    _LastProfilerType = ProfilerType.Prefix;
                                    foundProcess = true;
                                    break;
                                case "stackifymonitoringservice":
                                case "monitortestconsole":
                                    if(_LastProfilerType != ProfilerType.Prefix)
                                        _LastProfilerType = ProfilerType.APM;
                                    foundProcess = true;
                                    break;
                            }

                        }
                        catch
                        {
                            _ScanProcessException = true;
                        }

                    }
                }
                catch
                {
                    _ScanProcessException = true;
                }
            }
            

            //fall back to see if this has been set
            if (!foundProcess && _ScanProcessException)
            {
                var stackifyPath = Environment.GetEnvironmentVariable("StackifyPath");

                if (!string.IsNullOrEmpty(stackifyPath) && (stackifyPath.IndexOf("prefix", StringComparison.CurrentCultureIgnoreCase) > -1 || stackifyPath.IndexOf("devdash", StringComparison.CurrentCultureIgnoreCase) > -1))
                {
                    _LastProfilerType = ProfilerType.Prefix;
                }
                else if (!string.IsNullOrEmpty(stackifyPath))
                {
                    _LastProfilerType = ProfilerType.APM;
                }

            }

            return _LastProfilerType;
        }

        private static string Left(string sValue, int iMaxLength)
        {
            //Check if the value is valid
            if (string.IsNullOrEmpty(sValue))
            {
                //Set valid empty string as string could be null
                sValue = string.Empty;
            }
            else if (sValue.Length > iMaxLength)
            {
                //Make the string no longer than the max length
                sValue = sValue.Substring(0, iMaxLength);
            }

            //Return the string
            return sValue;
        }
    }
}
