using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace StackifyLib.Utils
{
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
                                case "stackifytracernotifier":
                                case "devdashtestconsole":
                                    _LastProfilerType = ProfilerType.Prefix;
                                    foundProcess = true;
                                    break;
                                case "stackifymonitoringservice":
                                case "monitortestconsole":
                                    _LastProfilerType = ProfilerType.APM;
                                    foundProcess = true;
                                    break;
                            }

                            if (foundProcess) break;
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
                var stackifyPath = Environment.GetEnvironmentVariable("StackifyPath", EnvironmentVariableTarget.Machine);

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
    }
}
