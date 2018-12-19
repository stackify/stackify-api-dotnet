using System;
using System.Collections.Generic;
using System.Linq;
using StackifyLib.Utils;

namespace StackifyLib
{
    /// <summary>
    /// Encapsulate settings retrieval mechanism. Currently supports config file and environment variables.
    /// Could be expanded to include other type of configuration servers later.
    /// </summary>
    public class Config
    {
#if NETCORE || NETCOREX

        private static Microsoft.Extensions.Configuration.IConfiguration _configuration = null;

        public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _configuration = configuration;
        }

#endif

        static Config()
        {
            LoadSettings();
        }

        public static void LoadSettings()
        {
            try
            {
                CaptureErrorPostdata = Get("Stackify.CaptureErrorPostdata", bool.FalseString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureServerVariables = Get("Stackify.CaptureServerVariables", bool.FalseString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureSessionVariables = Get("Stackify.CaptureSessionVariables", bool.FalseString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorHeaders = Get("Stackify.CaptureErrorHeaders", bool.TrueString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorCookies = Get("Stackify.CaptureErrorCookies", bool.FalseString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                ApiKey = Get("Stackify.ApiKey", ApiKey ?? string.Empty);

                AppName = Get("Stackify.AppName", AppName ?? string.Empty);

                Environment = Get("Stackify.Environment", Environment ?? string.Empty);

                CaptureErrorHeadersWhitelist = Get("Stackify.CaptureErrorHeadersWhitelist", string.Empty);

                if (string.IsNullOrEmpty(CaptureErrorHeadersWhitelist) == false)
                {
                    ErrorHeaderGoodKeys = CaptureErrorHeadersWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorHeadersBlacklist = Get("Stackify.CaptureErrorHeadersBlacklist", string.Empty);
                if (string.IsNullOrEmpty(CaptureErrorHeadersBlacklist) == false)
                {
                    ErrorHeaderBadKeys = CaptureErrorHeadersBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesWhitelist = Get("Stackify.CaptureErrorCookiesWhitelist", string.Empty);
                if (string.IsNullOrEmpty(CaptureErrorCookiesWhitelist) == false)
                {
                    ErrorCookiesGoodKeys = CaptureErrorCookiesWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesBlacklist = Get("Stackify.CaptureErrorCookiesBlacklist", string.Empty);
                if (string.IsNullOrEmpty(CaptureErrorCookiesBlacklist) == false)
                {
                    ErrorCookiesBadKeys = CaptureErrorCookiesBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorSessionWhitelist = Get("Stackify.CaptureErrorSessionWhitelist", string.Empty);
                if (string.IsNullOrEmpty(CaptureErrorSessionWhitelist) == false)
                {
                    ErrorSessionGoodKeys = CaptureErrorSessionWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // SF-6804: Frequent Calls to GetEC2InstanceId
                var captureEc2InstanceMetadataUpdateThresholdMinutes = Get("Stackify.Ec2InstanceMetadataUpdateThresholdMinutes", string.Empty);
                if (string.IsNullOrWhiteSpace(captureEc2InstanceMetadataUpdateThresholdMinutes) == false)
                {
                    if (int.TryParse(captureEc2InstanceMetadataUpdateThresholdMinutes, out int minutes) && minutes != 0)
                    {
                        Ec2InstanceMetadataUpdateThresholdMinutes = minutes;
                    }
                }

                // SF-6204: Allow local overrides of EC2 detection
                var isEc2 = Get("Stackify.IsEC2", string.Empty);
                if (string.IsNullOrWhiteSpace(isEc2) == false)
                {
                    IsEc2 = isEc2.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
                }

                // RT-297
                var apiLog = Get("Stackify.ApiLog", string.Empty);
                if (string.IsNullOrWhiteSpace(apiLog) == false)
                {
                    ApiLog = apiLog.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
                }

                var loggingMaxDepth = Get("Stackify.Logging.MaxDepth", "5");
                if (string.IsNullOrWhiteSpace(loggingMaxDepth) == false)
                {
                    if (int.TryParse(loggingMaxDepth, out var maxDepth) && maxDepth > 0 && maxDepth < 10)
                    {
                        LoggingMaxDepth = maxDepth;
                    }
                }

                var loggingMaxFields = Get("Stackify.Logging.MaxFields", "50");
                if (string.IsNullOrWhiteSpace(loggingMaxFields) == false)
                {
                    if (int.TryParse(loggingMaxFields, out var maxFields) && maxFields > 0 && maxFields < 100)
                    {
                        LoggingMaxFields = maxFields;
                    }
                }

                var loggingMaxStrLength = Get("Stackify.Logging.MaxStringLength", "32766");
                if (string.IsNullOrWhiteSpace(loggingMaxStrLength) == false)
                {
                    if (int.TryParse(loggingMaxStrLength, out var maxStrLen) && maxStrLen > 0 && maxStrLen < 32766)
                    {
                        LoggingMaxStringLength = maxStrLen;
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#Config #LoadSettings failed", ex);
            }
        }

        public static string ApiKey { get; set; }
        public static string Environment { get; set; }

        public static string AppName { get; set; }

        public static List<string> ErrorHeaderGoodKeys = new List<string>();
        public static List<string> ErrorHeaderBadKeys = new List<string>();
        public static List<string> ErrorCookiesGoodKeys = new List<string>();
        public static List<string> ErrorCookiesBadKeys = new List<string>();
        public static List<string> ErrorSessionGoodKeys = new List<string>();

        public static bool CaptureSessionVariables { get; set; }
        public static bool CaptureServerVariables { get; set; }
        public static bool CaptureErrorPostdata { get; set; }
        public static bool CaptureErrorHeaders { get; set; } = true;
        public static bool CaptureErrorCookies { get; set; }

        public static string CaptureErrorSessionWhitelist { get; set; }

        public static string CaptureErrorHeadersWhitelist { get; set; }

        public static string CaptureErrorHeadersBlacklist { get; set; } = "cookie,authorization";

        public static string CaptureErrorCookiesWhitelist { get; set; }

        public static string CaptureErrorCookiesBlacklist { get; set; } = ".ASPXAUTH";

        public static int Ec2InstanceMetadataUpdateThresholdMinutes { get; set; } = 60;

        public static bool? IsEc2 { get; set; }

        public static bool? ApiLog { get; set; }

        public static int LoggingMaxDepth { get; set; } = 5;

        public static int LoggingMaxFields { get; set; } = 50;

        public static int LoggingMaxStringLength { get; set; } = 32766;


        /// <summary>
        /// Attempts to fetch a setting value given the key.
        /// .NET configuration file will be used first, if the key is not found, environment variable will be used next.
        /// </summary>
        /// <param name="key">configuration key in config file or environment variable name.</param>
        /// <param name="defaultValue">If nothing is found, return optional defaultValue provided.</param>
        /// <returns>string value for the requested setting key.</returns>
        internal static string Get(string key, string defaultValue = null)
        {
            string v = null;

            try
            {
                if (string.IsNullOrWhiteSpace(key) == false)
                {
#if NETCORE || NETCOREX
                    if (_configuration != null)
                    {
                        var appSettings = _configuration.GetSection("Stackify");
                        v = appSettings[key.Replace("Stackify.", string.Empty)];
                    }
#endif

#if NETCOREX
                    if (_configuration != null)
                    {
                        var appSettings = _configuration.GetSection("Stackify");
                        v = appSettings[key.Replace("Stackify.", string.Empty)];
                    }
#endif

#if NETFULL
				    if (string.IsNullOrEmpty(v))
				    {
				        v = System.Configuration.ConfigurationManager.AppSettings[key];
				    }
#endif

                    if (string.IsNullOrEmpty(v))
                    {
                        v = System.Environment.GetEnvironmentVariable(key);
                    }
                }
            }
            finally
            {
                if (v == null)
                {
                    v = defaultValue;
                }
            }

            return v;
        }
    }
}
