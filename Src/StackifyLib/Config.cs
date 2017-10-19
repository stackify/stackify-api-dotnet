using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using StackifyLib.Utils;

namespace StackifyLib
{
	/// <summary>
	/// Encapsulate settings retrieval mechanism. Currently supports config file and environment variables.
	/// Could be expanded to include other type of configuration servers later.
	/// </summary>
	public class Config
	{
#if NETSTANDARD1_3 || NET451
        private static Microsoft.Extensions.Configuration.IConfigurationRoot _configuration = null;

	    public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfigurationRoot configuration)
	    {
	        _configuration = configuration;
	    }
#endif

        public static void LoadSettings()
	    {
	        try
	        {
                CaptureErrorPostdata = Get("Stackify.CaptureErrorPostdata", "").Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureServerVariables = Get("Stackify.CaptureServerVariables", "").Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureSessionVariables = Get("Stackify.CaptureSessionVariables", "").Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorHeaders = Get("Stackify.CaptureErrorHeaders", bool.TrueString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorCookies = Get("Stackify.CaptureErrorCookies", "").Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                ApiKey = Get("Stackify.ApiKey", "");

                AppName = Get("Stackify.AppName", "");

                Environment = Get("Stackify.Environment", "");

                CaptureErrorHeadersWhitelist = Get("Stackify.CaptureErrorHeadersWhitelist", "");

	            if (string.IsNullOrEmpty(CaptureErrorHeadersWhitelist) == false)
	            {
	                ErrorHeaderGoodKeys = CaptureErrorHeadersWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
	            }

                CaptureErrorHeadersBlacklist = Get("Stackify.CaptureErrorHeadersBlacklist", "");
                if (string.IsNullOrEmpty(CaptureErrorHeadersBlacklist) == false)
                {
                    ErrorHeaderBadKeys = CaptureErrorHeadersBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesWhitelist = Get("Stackify.CaptureErrorCookiesWhitelist", "");
                if (string.IsNullOrEmpty(CaptureErrorCookiesWhitelist) == false)
                {
                    ErrorCookiesGoodKeys = CaptureErrorCookiesWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesBlacklist = Get("Stackify.CaptureErrorCookiesBlacklist", "");
                if (string.IsNullOrEmpty(CaptureErrorCookiesBlacklist) == false)
                {
                    ErrorCookiesBadKeys = CaptureErrorCookiesBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorSessionWhitelist = Get("Stackify.CaptureErrorSessionWhitelist", "");
                if (string.IsNullOrEmpty(CaptureErrorSessionWhitelist) == false)
                {
                    ErrorSessionGoodKeys = CaptureErrorSessionWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // SF-6804: Frequent Calls to GetEC2InstanceId
                var captureEc2InstanceMetadataUpdateThresholdMinutes = Get("Stackify.Ec2InstanceMetadataUpdateThresholdMinutes", "");
                if (string.IsNullOrWhiteSpace(captureEc2InstanceMetadataUpdateThresholdMinutes) == false)
                {
                    if (int.TryParse(captureEc2InstanceMetadataUpdateThresholdMinutes, out int minutes) && minutes != 0)
                    {
                        Ec2InstanceMetadataUpdateThresholdMinutes = minutes;
                    }
                }

                // SF-6204: Allow local overrides of EC2 detection
                var isEc2 = Get("Stackify.IsEC2", "");
                if (string.IsNullOrWhiteSpace(isEc2) == false)
                {
                    IsEc2 = isEc2.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
                }

                // RT-297
	            var apiLog = Get("Stackify.ApiLog", "");
	            if (string.IsNullOrWhiteSpace(apiLog) == false)
	            {
	                ApiLog = apiLog.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
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

        public static bool CaptureSessionVariables { get; set; } = false;
        public static bool CaptureServerVariables { get; set; } = false;
        public static bool CaptureErrorPostdata { get; set; } = false;
        public static bool CaptureErrorHeaders { get; set; } = true;
        public static bool CaptureErrorCookies { get; set; } = false;

        public static string CaptureErrorSessionWhitelist { get; set; } = null;

        public static string CaptureErrorHeadersWhitelist { get; set; } = null;

        public static string CaptureErrorHeadersBlacklist { get; set; } = "cookie,authorization";

        public static string CaptureErrorCookiesWhitelist { get; set; } = null;

        public static string CaptureErrorCookiesBlacklist { get; set; } = ".ASPXAUTH";

        public static int Ec2InstanceMetadataUpdateThresholdMinutes { get; set; } = 60;

        public static bool? IsEc2 { get; set; } = null;

        public static bool? ApiLog { get; set; } = null;

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
				if (key != null)
				{
#if NETSTANDARD1_3 || NET451
                    if (_configuration != null)
                    {
                        var appSettings = _configuration.GetSection("Stackify");
                        v = appSettings[key.Replace("Stackify.", "")];
                    }
#endif

#if NET451 || NET45
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