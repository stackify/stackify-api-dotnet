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
#if NETSTANDARD1_3
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
                CaptureErrorPostdata = Get("Stackify.CaptureErrorPostdata", string.Empty).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureServerVariables = Get("Stackify.CaptureServerVariables", string.Empty).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureSessionVariables = Get("Stackify.CaptureSessionVariables", string.Empty).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorHeaders = Get("Stackify.CaptureErrorHeaders", bool.TrueString).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                CaptureErrorCookies = Get("Stackify.CaptureErrorCookies", string.Empty).Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);

                ApiKey = Get("Stackify.ApiKey", string.Empty);

                AppName = Get("Stackify.AppName", string.Empty);

                Environment = Get("Stackify.Environment", string.Empty);

                CaptureErrorHeadersWhitelist = Get("Stackify.CaptureErrorHeadersWhitelist", string.Empty);

	            if (!string.IsNullOrEmpty(CaptureErrorHeadersWhitelist))
	            {
	                ErrorHeaderGoodKeys = CaptureErrorHeadersWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
	            }

                CaptureErrorHeadersBlacklist = Get("Stackify.CaptureErrorHeadersBlacklist", string.Empty);
                if (!string.IsNullOrEmpty(CaptureErrorHeadersBlacklist))
                {
                    ErrorHeaderBadKeys = CaptureErrorHeadersBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesWhitelist = Get("Stackify.CaptureErrorCookiesWhitelist", string.Empty);
                if (!string.IsNullOrEmpty(CaptureErrorCookiesWhitelist))
                {
                    ErrorCookiesGoodKeys = CaptureErrorCookiesWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorCookiesBlacklist = Get("Stackify.CaptureErrorCookiesBlacklist", string.Empty);
                if (!string.IsNullOrEmpty(CaptureErrorCookiesBlacklist))
                {
                    ErrorCookiesBadKeys = CaptureErrorCookiesBlacklist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                CaptureErrorSessionWhitelist = Get("Stackify.CaptureErrorSessionWhitelist", string.Empty);
                if (!string.IsNullOrEmpty(CaptureErrorSessionWhitelist))
                {
                    ErrorSessionGoodKeys = CaptureErrorSessionWhitelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                var isEc2 = Get("Stackify.IsEC2", string.Empty);
                if (string.IsNullOrWhiteSpace(isEc2) == false)
                {
                    IsEc2 = isEc2.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
                }

                ApiHost = Get("Stackify.ApiHost", "https://api.stackify.net");
                AuthTokenUrl = Get("Stackify.AuthTokenUrl", "https://auth.stackify.net/oauth2/token");
                LogUri = Get("Stackify.LogUri", "api/v1/logs");

                // RT-297
	            var apiLog = Get("Stackify.ApiLog", string.Empty);
	            if (string.IsNullOrWhiteSpace(apiLog) == false)
	            {
	                ApiLog = apiLog.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
	            }
            }
            catch (Exception ex)
	        {
	            StackifyAPILogger.Log("#Config #LoadSettings", ex);
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

        public static bool? IsEc2 { get; set; } = null;

	    public static bool? ApiLog { get; set; } = null;

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

#if NETSTANDARD1_3
                    if (_Configuration != null)
                    {
                        var appSettings = _Configuration.GetSection("Stackify");
                        v = appSettings[key.Replace("Stackify.", string.Empty)];
                    }
#endif

#if NETFULL
                    v = string.IsNullOrEmpty(v) ? System.Configuration.ConfigurationManager.AppSettings[key] : v;
#endif
                    v = string.IsNullOrEmpty(v) ? System.Environment.GetEnvironmentVariable(key) : v;
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
