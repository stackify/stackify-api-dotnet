﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace StackifyLib
{
	/// <summary>
	/// Encapsulate settings retrieval mechanism. Currently supports config file and environment variables.
	/// Could be expanded to include other type of configuration servers later.
	/// </summary>
	public class Config
	{

#if NETSTANDARD1_3 || NET451
        private static Microsoft.Extensions.Configuration.IConfiguration _Configuration = null;

	    public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
	    {
	        _Configuration = configuration;
	    }
#endif
        public static void LoadSettings()
	    {
	        try
	        {
                CaptureErrorPostdata = Get("Stackify.CaptureErrorPostdata", "")
                    .Equals("true", StringComparison.CurrentCultureIgnoreCase);

                CaptureServerVariables = Get("Stackify.CaptureServerVariables", "")
                    .Equals("true", StringComparison.CurrentCultureIgnoreCase);
                CaptureSessionVariables = Get("Stackify.CaptureSessionVariables", "")
                    .Equals("true", StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorHeaders = Get("Stackify.CaptureErrorHeaders", "true").Equals("true", StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorCookies = Get("Stackify.CaptureErrorCookies", "")
                    .Equals("true", StringComparison.CurrentCultureIgnoreCase);

                ApiKey = Get("Stackify.ApiKey", "");

                AppName = Get("Stackify.AppName", "");

                Environment = Get("Stackify.Environment", "");

                CaptureErrorHeadersWhitelist = Get("Stackify.CaptureErrorHeadersWhitelist", "");

	            if (!string.IsNullOrEmpty(CaptureErrorHeadersWhitelist))
	            {
	                ErrorHeaderGoodKeys = CaptureErrorHeadersWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
	            }

                CaptureErrorHeadersBlacklist = Get("Stackify.CaptureErrorHeadersBlacklist", "");
                if (!string.IsNullOrEmpty(CaptureErrorHeadersBlacklist))
                {
                    ErrorHeaderBadKeys = CaptureErrorHeadersBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesWhitelist = Get("Stackify.CaptureErrorCookiesWhitelist", "");
                if (!string.IsNullOrEmpty(CaptureErrorCookiesWhitelist))
                {
                    ErrorCookiesGoodKeys = CaptureErrorCookiesWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesBlacklist = Get("Stackify.CaptureErrorCookiesBlacklist", "");
                if (!string.IsNullOrEmpty(CaptureErrorCookiesBlacklist))
                {
                    ErrorCookiesBadKeys = CaptureErrorCookiesBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorSessionWhitelist = Get("Stackify.CaptureErrorSessionWhitelist", "");
                if (!string.IsNullOrEmpty(CaptureErrorSessionWhitelist))
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
                    IsEc2 = isEc2.Equals("true", StringComparison.CurrentCultureIgnoreCase);
                }

                ApiHost = Get("Stackify.ApiHost", "https://api.stackify.net");
                AuthTokenUrl = Get("Stackify.AuthTokenUrl", "https://auth.stackify.net/oauth2/token");
                LogUri = Get("Stackify.LogUri", "api/v1/logs");
            }
            catch (Exception ex)
	        {
	            Debug.WriteLine(ex.ToString());
	        }
        }

        public static string AuthTokenUrl { get; set; }
        public static string ApiHost { get; set; }
        public static string LogUri { get; set; }

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

        /// <summary>
        /// Global setting for any log appenders for how big the log queue size can be in memory
        /// before messages are lost if there are problems uploading or we can't upload fast enough
        /// </summary>
        public static int MaxLogBufferSize { get; set; }  = 10000;

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
                    if (_Configuration != null)
                    {
                        var appSettings = _Configuration.GetSection("Stackify");
                        v = appSettings[key.Replace("Stackify.", "")];
                    }
#endif

#if NET451 || NET45
                    v = string.IsNullOrEmpty(v) ? System.Configuration.ConfigurationManager.AppSettings[key] : v;
#endif
                    v = string.IsNullOrEmpty(v) ? System.Environment.GetEnvironmentVariable(key) : v;
                }
            }
			finally
			{
				if (v == null)
					v = defaultValue;
			}
			return v;
		}
	}
}
