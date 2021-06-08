using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackifyLib.Utils;

namespace StackifyLib.Web
{
    public static class RealUserMonitoring
    {
        public static string GetHeaderScript()
        {
            var settings = new JObject();
            var reqId = HelperFunctions.GetRequestId();
            if (reqId != null)
            {
                settings["ID"] = reqId;
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
                settings["Trans"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(reportingUrl));
            }

            return string.Format(@"<script type=""text/javascript"">var StackifySettings = {0};</script>", settings.ToString(Formatting.None));
        }
    }
}
