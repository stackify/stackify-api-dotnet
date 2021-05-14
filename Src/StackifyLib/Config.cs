using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using StackifyLib.Utils;

namespace StackifyLib
{
    /// <summary>
    /// Encapsulate settings retrieval mechanism. Currently supports config file and environment variables.
    /// Could be expanded to include other type of configuration servers later.
    /// </summary>
    public class Config
    {

        private static readonly JsonLoadSettings Settings = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore, LineInfoHandling = LineInfoHandling.Ignore };
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
                if (IsStackifyJsonLoaded == false) {
                    ReadStackifyJSONConfig(); // TODO: Better way?
                }

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

                var loggingJsonMaxFields = Get("Stackify.Logging.JsonMaxFields", "50");
                if (string.IsNullOrWhiteSpace(loggingJsonMaxFields) == false)
                {
                    if (int.TryParse(loggingJsonMaxFields, out int maxFields) && maxFields > 0 && maxFields < 100)
                    {
                        LoggingJsonMaxFields = maxFields;
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

        public static int LoggingJsonMaxFields { get; set; } = 50;

        public static bool? IsStackifyJsonLoaded { get; set; } = false;


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

                        //Get settings from Stackify.json
                        if (string.IsNullOrEmpty(v))
                        {
                            var key2 = key.Replace("Stackify.", string.Empty);
                            var stackifyJson = _configuration.GetSection(key2);
                            v = stackifyJson.Value;
                        }
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

        public static void ReadStackifyJSONConfig()
        {
            try
            {
                string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string jsonPath = Path.Combine(baseDirectory, "Stackify.json");
                string json;

                if (File.Exists(jsonPath))
                {
                    using (var fs = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var sr = new StreamReader(fs))
                        {
                            json = sr.ReadToEnd();
                        }
                    }
                    var obj = JObject.Parse(json, Settings);
                    Config.SetStackifyObj(obj);
                }
#if NETFULL
                else
                {
                    string iisBaseDirectory = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
                    string iisJsonPath = Path.Combine(iisBaseDirectory, "Stackify.json");

                    if (File.Exists(iisJsonPath))
                    {
                        using (var fs = new FileStream(iisJsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (var sr = new StreamReader(fs))
                            {
                                json = sr.ReadToEnd();
                            }
                        }

                        var obj = JObject.Parse(json, Settings);
                        Config.SetStackifyObj(obj);
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#Config #ReadStackifyJSONConfig failed", ex);
            }
            IsStackifyJsonLoaded = true;
        }

#if JSONTEST
        public static void ReadStackifyJSONConfig(string filePath)
        {
            try
            {
                string json;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var sr = new StreamReader(fs))
                    {
                        json = sr.ReadToEnd();
                    }
                }

                var obj = JObject.Parse(json, Settings);
                Config.SetStackifyObj(obj);
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#Config #ReadStackifyJSONConfig failed", ex);
            }
            
        }
#endif

        public static void SetStackifyObj(JObject obj)
        {
            AppName = TryGetValue(obj, "AppName") ?? AppName;
            Environment = GetEnvironment(obj);
            ApiKey = TryGetValue(obj, "ApiKey") ?? ApiKey;
        }

        private static string TryGetValue(JToken jToken, string key)
        {
            string r = null;

            try
            {
                var val = jToken[key];
                if (val == null)
                {
                    StackifyAPILogger.Log($"#Config #TryGetValue #Json failed - Property is null or empty - Property: {key}");
                    return r;
                }

                if (val.Type != JTokenType.String || (val.Type == JTokenType.String && string.IsNullOrEmpty(val.ToString())))
                {
                    StackifyAPILogger.Log($"#Config #TryGetValue #Json failed - Property is not a string - Property: {key}");
                    return r;
                }

                r = val.ToString();
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("#Config #TryGetValue #Json failed", ex);
            }

            return r;
        }
        public static string GetEnvironment(JObject envName = null)
        {
            var ASPEnvironment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var DotnetEnvironment = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            if (!String.IsNullOrEmpty(ASPEnvironment))
            {
                return ASPEnvironment;
            }
            else if (!String.IsNullOrEmpty(DotnetEnvironment))
            {
                return DotnetEnvironment;
            }
            else
            {
                var environmentName = TryGetValue(envName, "Environment") ?? Environment;

                return environmentName;
            }
            
        }
    }
}
