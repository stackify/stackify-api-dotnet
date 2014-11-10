using System;
using System.Configuration;
using System.Linq;
using System.Management;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using StackifyLib.Utils;
using System.Web;

namespace StackifyLib.Models
{
    [DataContract]
    public class EnvironmentDetail
    {

        private static EnvironmentDetail _CachedCopy = null;

        public static EnvironmentDetail Get(bool refresh)
        {
            if (refresh || _CachedCopy == null)
            {
                _CachedCopy = new EnvironmentDetail();
            }

            return _CachedCopy;
        }

        private bool registryAccessFailure = false;

        /// <summary>
        /// Figures out if the server is in azure and if so gets the azure instance name
        /// </summary>
        private void GetAzureInfo()
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

                                string deploymentKeyPath = versionKeyPath + "\\" + deploymentID;
                                using (var deploymentKey = RegistryHelper.GetRegistryKey(deploymentKeyPath))
                                {
                                    var roles = deploymentKey.GetSubKeyNames();

                                    if (roles.Any())
                                    {
                                        this.AppName = roles.First();
                                        string roleKeyPath = deploymentKeyPath + "\\" + this.AppName;

                                        using (var roleKey = RegistryHelper.GetRegistryKey(roleKeyPath))
                                        {
                                            var instances = roleKey.GetSubKeyNames();

                                            this.AzureInstanceName = instances.First();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Security.SecurityException ex)
            {
                registryAccessFailure = true;
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }
        }

        /// <summary>
        /// Get the display name of the windows service if it is a windows service
        /// </summary>
        private void IsWindowService()
        {
            try
            {
                string query = "select DisplayName from Win32_Service WHERE ProcessID='" + System.Diagnostics.Process.GetCurrentProcess().Id + "'";

                var wmiQuery = new ObjectQuery(query);
                ManagementScope managementScope = new ManagementScope();

                using (var searcher = new ManagementObjectSearcher(managementScope, wmiQuery))
                {
                    using (ManagementObjectCollection mObjects = searcher.Get())
                    {

                        foreach (ManagementObject queryObj in mObjects)
                        {
                            object o = queryObj.GetPropertyValue("DisplayName");
                            if (o != null)
                            {
                                this.AppName = o.ToString();
                            }

                            queryObj.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Unable to get windows service name\r\n" + ex.ToString(), true);
            }
        }

        private EnvironmentDetail()
        {

            bool isWebRequest = false;

            try
            {
                isWebRequest = AppDomain.CurrentDomain.FriendlyName.Contains("W3SVC");

                if (isWebRequest)
                {
                    //regex test cases
                    /*
                 /LM/W3SVC/1/ROOT/OpsManager-1-130294385752072113
                /LM/W3SVC/1/ROOT/Apps/StackifyAPI-3-130294479694571305
                /LM/W3SVC/1/ROOT/Apps/Num bers-3-4-130294481053328340
                 */

                    //var match = Regex.Match(AppDomain.CurrentDomain.FriendlyName, "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT/(?<appname>[^\\/-]+)-");
                    var match = Regex.Match(AppDomain.CurrentDomain.FriendlyName,
                        "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT/(?<appname>.+)-[0-9]{1}-[\\d]{2,}$");


                    if (match.Success)
                    {
                        WebAppID = string.Format("W3SVC_{0}_ROOT_{1}", match.Groups["siteid"].Value,
                            match.Groups["appname"].Value.Replace("/", "_"));
                    }


                    match = Regex.Match(AppDomain.CurrentDomain.FriendlyName,
                        "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT-[0-9]{1}-[\\d]{2,}$");


                    if (match.Success)
                    {
                        WebAppID = string.Format("W3SVC_{0}_ROOT", match.Groups["siteid"].Value);
                    }


                    if (WebAppID == null)
                    {
                        //just putting it here so we can get some visibility to it
                        WebAppID = AppDomain.CurrentDomain.FriendlyName;
                    }
                }


            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }

    

            try
            {
                this.IsAzureWorkerRole = false;

                //Logger global properties would override everything

                if (!string.IsNullOrEmpty(Logger.GlobalAppName))
                {
                    ConfiguredAppName = Logger.GlobalAppName;
                }
                else
                {
                    ConfiguredAppName = ConfigurationManager.AppSettings["Stackify.AppName"];    
                }


                if (!string.IsNullOrEmpty(Logger.GlobalEnvironment))
                {
                    ConfiguredEnvironmentName = Logger.GlobalEnvironment;
                }
                else
                {
                    ConfiguredEnvironmentName = ConfigurationManager.AppSettings["Stackify.Environment"];
                }

                //might be azure server. Check
                if (Environment.MachineName.StartsWith("RD"))
                {
                    GetAzureInfo();
                }

                //Not a web app, check for windows service
                if (!Environment.UserInteractive && !AppDomain.CurrentDomain.FriendlyName.Contains("W3SVC"))
                {
                    IsWindowService();
                }

                DeviceName = Environment.MachineName;

                if (string.IsNullOrEmpty(AppName) && !isWebRequest)
                {
                    AppName = AppDomain.CurrentDomain.FriendlyName;
                }

                AppLocation = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

                //is it in azure worker role? If so tweak the location
                if (AppDomain.CurrentDomain.FriendlyName.Equals("RdRuntime", StringComparison.OrdinalIgnoreCase) && AppLocation.IndexOf("approot", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    this.IsAzureWorkerRole = true;

                    AppLocation = AppLocation.Replace("RdRuntime", "").TrimEnd('\\');
                }
                else if (!isWebRequest)
                {
                    AppLocation += "\\" + AppDomain.CurrentDomain.FriendlyName; //add the file name on the end
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }

            try
            {
                
                
                //if we do not have an appname still, use the app path or folder name
                if (string.IsNullOrEmpty(AppName) && isWebRequest && HostingEnvironment.ApplicationVirtualPath != null)
                {
                    if (string.IsNullOrWhiteSpace(HostingEnvironment.ApplicationVirtualPath) || HostingEnvironment.ApplicationVirtualPath == "/")
                    {
                        if (AppLocation != null)
                        {
                            AppName = AppLocation.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Last();
                        }
                    }
                    else
                    {
                        string[] paths = HostingEnvironment.ApplicationVirtualPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        AppName = paths.Last();
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }
           
        }

        [DataMember]
        public string DeviceName { get; set; }
        [DataMember]
        public string AppLocation { get; set; }
        [DataMember]
        public string AppName { get; set; }
        [DataMember]
        public string WebAppID { get; set; }
        [DataMember]
        public string ConfiguredAppName { get; set; }
        [DataMember]
        public string ConfiguredEnvironmentName { get; set; }
        [DataMember]
        public bool IsAzureWorkerRole { get; set; }

        [IgnoreDataMember]
        public string AzureInstanceName { get; set; }

        public string AppNameToUse()
        {
            if (!string.IsNullOrWhiteSpace(ConfiguredAppName))
            {
                return ConfiguredAppName;
            }

            return AppName;
        }
    }
}
