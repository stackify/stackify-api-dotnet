using System;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackifyLib.Utils;

namespace StackifyLib.Web
{
    public static class RealUserMonitoring
    {
        private static readonly RandomNumberGenerator Rng = new RNGCryptoServiceProvider();
        
        /// <summary>
        /// Generate the header script for including RUM
        /// </summary>
        /// <param name="nonce">nonce value, defaults to a cryptographic unique string if left null</param>
        public static string GetHeaderScript(string nonce = null)
        {
            var rumScriptUrl = Config.RumScriptUrl;
            var rumKey = Config.RumKey;

            if (string.IsNullOrWhiteSpace(rumScriptUrl) || string.IsNullOrWhiteSpace(rumKey))
            {
                // If we don't have a key and url, don't insert the script
                return "";
            }

            var settings = new JObject();
            var reqId = HelperFunctions.GetRequestId();
            if (reqId != null)
            {
                settings["ID"] = reqId;
            }
            else
            {
                // If there is no request ID, don't write the script
                return "";
            }

            var appName = HelperFunctions.GetAppName();
            if (!string.IsNullOrWhiteSpace(appName))
            {
                settings["Name"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(appName));
            }

            var environment = HelperFunctions.GetAppEnvironment();
            if (!string.IsNullOrWhiteSpace(environment))
            {
                settings["Env"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(environment));
            }

            var reportingUrl = HelperFunctions.GetReportingUrl();
            if (reportingUrl != null)
            {
                reportingUrl = HelperFunctions.MaskReportingUrl(reportingUrl);
                settings["Trans"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(reportingUrl));
            }

            return string.Format("<script type=\"text/javascript\" nonce=\"{3}\">(window.StackifySettings || (window.StackifySettings = {0}))</script><script src=\"{1}\" data-key=\"{2}\" async></script>",
                settings.ToString(Formatting.None), rumScriptUrl, rumKey, nonce ?? GetNonce());
        }

        // generate nonce for strict CSP rules
        private static string GetNonce()
        {
            var nonceBytes = new byte[20];
            Rng.GetNonZeroBytes(nonceBytes);
            return Convert.ToBase64String(nonceBytes);
        }
    }
}
