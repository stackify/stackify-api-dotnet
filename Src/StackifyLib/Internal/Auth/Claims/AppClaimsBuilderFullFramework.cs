#if NETFULL
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StackifyLib.Utils;
using System.Linq;
using System.Web.Hosting;
using System.Management;

namespace StackifyLib.Internal.Auth.Claims
{
    internal class AppClaimsBuilderFullFramework : AppClaimsBuilderBase
    {
        private static bool registryAccessFailure = false;

        protected override async Task BuildClaimsAsync()
        {
            try
            {
                SetWebAppId();
                AppClaims.IsAzureWorkerRole = false;
                AppClaims.ConfiguredAppName = string.IsNullOrEmpty(Config.AppName) ? Config.Get("Stackify.AppName") : Config.AppName;
                AppClaims.ConfiguredEnvironmentName = string.IsNullOrEmpty(Config.Environment) ? Config.Get("Stackify.Environment") : Config.Environment;

                // might be azure server. If it is, get the AppName from that
                SetAzureInfo();
                SetWindowServiceAppName();
                await SetDeviceName();
                SetClaimsFromAppDomain();
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }

            try
            {
                // if we do not have an appname still, use the app path or folder name
                SetClaimsFromPath();
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }
        }

        private void SetWebAppId()
        {
            IsWebRequest = AppDomain.CurrentDomain.FriendlyName.Contains("W3SVC");

            if (IsWebRequest == false)
                return;

            //regex test cases
            /*
                /LM/W3SVC/1/ROOT/OpsManager-1-130294385752072113
                /LM/W3SVC/1/ROOT/Apps/StackifyAPI-3-130294479694571305
                /LM/W3SVC/1/ROOT/Apps/Num bers-3-4-130294481053328340
            */

            var match = Regex.Match(AppDomain.CurrentDomain.FriendlyName,
                "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT/(?<appname>.+)-[0-9]{1,3}-[\\d]{2,}$");

            if (match.Success)
            {
                AppClaims.WebAppID = string.Format("W3SVC_{0}_ROOT_{1}", match.Groups["siteid"].Value,
                    match.Groups["appname"].Value.Replace("/", "_"));
            }

            match = Regex.Match(AppDomain.CurrentDomain.FriendlyName,
                "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT-[0-9]{1,3}-[\\d]{2,}$");

            if (match.Success)
            {
                AppClaims.WebAppID = string.Format("W3SVC_{0}_ROOT", match.Groups["siteid"].Value);
            }

            if (AppClaims.WebAppID == null)
            {
                // just putting it here so we can get some visibility to it
                AppClaims.WebAppID = AppDomain.CurrentDomain.FriendlyName;
            }
        }

        /// <summary>
        /// Figures out if the server is in azure and if so gets the azure instance name
        /// </summary>
        private void SetAzureInfo()
        {
            if (registryAccessFailure)
                return;

            try
            {
                string rootKey = @"SOFTWARE\Microsoft\Windows Azure\Deployments";

                using (var root = RegistryHelper.GetRegistryKey(rootKey))
                {
                    if (root != null)
                    {
                        var versions = root.GetSubKeyNames();
                        string versionKeyPath = rootKey + "\\" + versions.First();
                        using (var versionKey = RegistryHelper.GetRegistryKey(versionKeyPath))
                        {
                            var deployments = versionKey.GetSubKeyNames();

                            if (deployments.Any())
                            {
                                string deploymentID = deployments.First();

                                Guid g;
                                bool validDeploymentID = Guid.TryParse(deploymentID, out g);
                                if (!validDeploymentID)
                                {
                                    return;
                                }

                                string deploymentKeyPath = versionKeyPath + "\\" + deploymentID;
                                using (var deploymentKey = RegistryHelper.GetRegistryKey(deploymentKeyPath))
                                {
                                    var roles = deploymentKey.GetSubKeyNames();

                                    if (roles.Any())
                                    {
                                        AppClaims.AppName = roles.First();
                                        string roleKeyPath = deploymentKeyPath + "\\" + AppClaims.AppName;

                                        using (var roleKey = RegistryHelper.GetRegistryKey(roleKeyPath))
                                        {
                                            var instances = roleKey.GetSubKeyNames();

                                            AppClaims.AzureInstanceName = instances.First();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
                registryAccessFailure = true;
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error seeing if the app is an azure cloud service\r\n" + ex.ToString(), true);
            }
        }

        /// <summary>
        /// Get the display name of the windows service if it is a windows service
        /// </summary>
        private void SetWindowServiceAppName()
        {
            if (Environment.UserInteractive || !AppDomain.CurrentDomain.FriendlyName.Contains("W3SVC"))
                return;

            try
            {
                var query = $"select DisplayName from Win32_Service WHERE ProcessID='{Process.GetCurrentProcess().Id}'";
                var wmiQuery = new ObjectQuery(query);
                var managementScope = new ManagementScope();

                using (var searcher = new ManagementObjectSearcher(managementScope, wmiQuery))
                {
                    using (ManagementObjectCollection mObjects = searcher.Get())
                    {

                        foreach (ManagementObject queryObj in mObjects)
                        {
                            object o = queryObj.GetPropertyValue("DisplayName");
                            if (o != null)
                            {
                                AppClaims.AppName = o.ToString();
                            }

                            queryObj.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Unable to get windows service name\r\n" + ex.ToString(), true);
            }
        }

        /// <summary>
        /// Get the EC2 Instance name if it exists else null
        /// </summary>
        protected override Task<string> GetEC2InstanceId()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(EC2InstanceIdUrl);

                // wait 5 seconds
                request.Timeout = 5000;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                    {
                        var encoding = Encoding.GetEncoding(response.CharacterSet);
                        using (var responseStream = response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(responseStream, encoding))
                            {
                                var id = reader.ReadToEnd();
                                var r = string.IsNullOrWhiteSpace(id) ? null : id;
                                return Task.FromResult(r);
                            }
                        }
                    }
                }
            }
            catch // if not in aws this will timeout
            { }

            return Task.FromResult<string>(null);
        }

        /// <summary>
        /// Get the current machine name
        /// </summary>
        protected override string GetMachineName()
        {
            var machineName = Environment.MachineName;
            return machineName;
        }

        private void SetClaimsFromAppDomain()
        {
            if (string.IsNullOrEmpty(AppClaims.AppName) && !IsWebRequest)
            {
                AppClaims.AppName = AppDomain.CurrentDomain.FriendlyName;
            }

            AppClaims.AppLocation = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

            // is it in azure worker role? If so tweak the location
            if (AppDomain.CurrentDomain.FriendlyName.Equals("RdRuntime", StringComparison.OrdinalIgnoreCase)
                && AppClaims.AppLocation.IndexOf("approot", StringComparison.OrdinalIgnoreCase) > 0)
            {
                AppClaims.IsAzureWorkerRole = true;
                AppClaims.AppLocation = AppClaims.AppLocation.Replace("RdRuntime", "").TrimEnd('\\');
            }
            else if (!IsWebRequest)
            {
                AppClaims.AppLocation += "\\" + AppDomain.CurrentDomain.FriendlyName; //add the file name on the end
            }
        }

        private void SetClaimsFromPath()
        {
            if (string.IsNullOrEmpty(AppClaims.AppName) && IsWebRequest && HostingEnvironment.ApplicationVirtualPath != null)
            {
                if (string.IsNullOrWhiteSpace(HostingEnvironment.ApplicationVirtualPath) || HostingEnvironment.ApplicationVirtualPath == "/")
                {
                    if (AppClaims.AppLocation != null)
                    {
                        AppClaims.AppName = AppClaims.AppLocation.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Last();
                    }
                }
                else
                {
                    var paths = HostingEnvironment.ApplicationVirtualPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    AppClaims.AppName = paths.Last();
                }
            }
        }
    }
}
#endif