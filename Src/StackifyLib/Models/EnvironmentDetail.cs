using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackifyLib.Utils;
#if NETFULL
using System.Linq;
using System.Web.Hosting;
using System.Management;
#endif

namespace StackifyLib.Models
{
    public class EnvironmentDetail
    {
        private static EnvironmentDetail _CachedCopy = null;

        public static EnvironmentDetail Get()
        {
            return _CachedCopy ?? (_CachedCopy = new EnvironmentDetail());
        }
#if NETSTANDARD
        private static System.Net.Http.HttpClient Client => new System.Net.Http.HttpClient();
#endif
        private static bool registryAccessFailure = false;

        /// <summary>
        /// Figures out if the server is in azure and if so gets the azure instance name
        /// </summary>
        private void GetAzureInfo()
        {
#if NETFULL
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
                StackifyLib.Utils.StackifyAPILogger.Log("Error seeing if the app is an azure cloud service\r\n" + ex.ToString(), true);
            }
#endif
        }

        // http://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-metadata.html#d0e30002
        private const string EC2InstanceIdUrl = "http://169.254.169.254/latest/meta-data/instance-id";
        public static readonly object ec2InstanceLock = new object();
        private static DateTimeOffset? ec2InstanceIdLastUpdate = null;
        private static string ec2InstanceId = null;

        /// <summary>
        /// Get the EC2 Instance name if it exists else null
        /// </summary>
#if NETFULL

        public static string GetDeviceName()
        {
            var deviceName = Environment.MachineName;
            var deviceNodeName = Environment.GetEnvironmentVariable("STACKIFY_DEVICE_NAME");

            if (!String.IsNullOrEmpty(deviceNodeName))
            {
                deviceName = deviceNodeName;
            }
            else
            {
                if (AzureConfig.InAzure && ((AzureConfig.IsWebsite) || (AzureConfig.InAzure && Environment.MachineName.StartsWith("RD"))))
                {
                    deviceName = AzureConfig.AzureInstanceName;
                }
                else
                {
                    var isDefaultDeviceNameEc2 = IsEc2MachineName(deviceName);

                    if (Config.IsEc2 == null || Config.IsEc2 == true || isDefaultDeviceNameEc2)
                    {
                        var ec2InstanceId = GetEC2InstanceId();
                        if (string.IsNullOrWhiteSpace(ec2InstanceId) == false)
                        {
                            deviceName = ec2InstanceId;
                        }
                    }
                }
            }

            return deviceName.Substring(0, deviceName.Length > 60 ? 60 : deviceName.Length);
        }

        public static string GetEC2InstanceId()
        {
            string r = null;

            // SF-6804: Frequent Calls to GetEC2InstanceId
            bool skipEc2InstanceIdUpdate = false;
            if (Config.Ec2InstanceMetadataUpdateThresholdMinutes > 0)
            {
                var threshold = TimeSpan.FromMinutes(Config.Ec2InstanceMetadataUpdateThresholdMinutes);
                lock (ec2InstanceLock)
                {
                    skipEc2InstanceIdUpdate = ec2InstanceIdLastUpdate != null && ec2InstanceIdLastUpdate < DateTimeOffset.UtcNow.Subtract(threshold);
                    r = string.IsNullOrWhiteSpace(ec2InstanceId) ? null : ec2InstanceId;
                }
            }
            else
            {
                skipEc2InstanceIdUpdate = true;
            }

            if (skipEc2InstanceIdUpdate)
            {
                return r;
            }

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
                                r = string.IsNullOrWhiteSpace(id) ? null : id;
                            }
                        }
                    }
                }
            }
            catch // if not in aws this will timeout
            {
                r = null;
            }

            lock (ec2InstanceLock)
            {
                ec2InstanceId = r;
                ec2InstanceIdLastUpdate = DateTimeOffset.UtcNow;
            }

            return r;
        }
#else
        public static string GetDeviceName()
        {
            var deviceName = Environment.MachineName;
            var deviceNodeName = Environment.GetEnvironmentVariable("STACKIFY_DEVICE_NAME");

            if (!String.IsNullOrEmpty(deviceNodeName))
            {
                deviceName = deviceNodeName;
            }
            else
            {
                if (AzureConfig.InAzure && ((AzureConfig.IsWebsite) || (AzureConfig.InAzure && Environment.MachineName.StartsWith("RD"))))
                {
                    deviceName = AzureConfig.AzureInstanceName;
                }
                else
                {
                    var isDefaultDeviceNameEc2 = IsEc2MachineName(deviceName);

                    if (Config.IsEc2 == null || Config.IsEc2 == true || isDefaultDeviceNameEc2)
                    {
                        var instanceID_task = GetEC2InstanceId();
                        instanceID_task.Wait();
                        if (string.IsNullOrWhiteSpace(instanceID_task.Result) == false)
                        {
                            deviceName = instanceID_task.Result;
                        }
                    }
                }
            }
            return deviceName.Substring(0, deviceName.Length > 60 ? 60 : deviceName.Length);
        }

        public static async Task<string> GetEC2InstanceId()
        {
            string r = null;

            try
            {
                Client.Timeout = TimeSpan.FromSeconds(5);
                var content = await Client.GetAsync(EC2InstanceIdUrl);

                int statusCode = (int)content.StatusCode;

                if (statusCode >= 200 && statusCode < 300)
                {
                    string id = await content.Content.ReadAsStringAsync();
                    r = string.IsNullOrWhiteSpace(id) ? null : id;

                    if (r.Contains("html"))
                    {
                        r = Environment.MachineName;
                    }
                }

            }
            catch // if not in aws this will timeout
            {
                r = null;
            }

            lock (ec2InstanceLock)
            {
                ec2InstanceId = r;
                ec2InstanceIdLastUpdate = DateTimeOffset.UtcNow;
            }

            return r;
        }

#endif
        private static bool IsEc2MachineName(string machineName)
        {
            if (string.IsNullOrWhiteSpace(machineName))
            {
                return false;
            }

            if (machineName.StartsWith("EC2") && machineName.Contains("-"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the display name of the windows service if it is a windows service
        /// </summary>
        private void IsWindowService()
        {
#if NETFULL
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
#endif
        }

        public EnvironmentDetail()
        {

            bool isWebRequest = false;
#if NETFULL
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
                        "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT/(?<appname>.+)-[0-9]{1,3}-[\\d]{2,}$");


                    if (match.Success)
                    {
                        WebAppID = string.Format("W3SVC_{0}_ROOT_{1}", match.Groups["siteid"].Value,
                            match.Groups["appname"].Value.Replace("/", "_"));
                    }


                    match = Regex.Match(AppDomain.CurrentDomain.FriendlyName,
                        "/LM/W3SVC/(?<siteid>[\\d]+)/ROOT-[0-9]{1,3}-[\\d]{2,}$");


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

#endif

            try
            {
                this.IsAzureWorkerRole = false;

                //Logger global properties would override everything

                if (!string.IsNullOrEmpty(Config.AppName))
                {
                    ConfiguredAppName = Config.AppName;
                }
                else
                {
                    ConfiguredAppName = Config.Get("Stackify.AppName");
                }


                if (!string.IsNullOrEmpty(Config.Environment))
                {
                    ConfiguredEnvironmentName = Config.Environment;
                }
                else
                {
                    ConfiguredEnvironmentName = Config.Get("Stackify.Environment");
                }

                //might be azure server. If it is, get the AppName from that
                GetAzureInfo();


#if NETFULL
                //Not a web app, check for windows service
                if (!Environment.UserInteractive && !AppDomain.CurrentDomain.FriendlyName.Contains("W3SVC"))
                {
                    IsWindowService();
                }
#endif

                DeviceName = GetDeviceName();

#if NETFULL
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
#else
                AppLocation = AppContext.BaseDirectory;
#endif

            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }

#if NETFULL
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
#endif
        }

        [JsonProperty]
        public string DeviceName { get; set; }
        [JsonProperty]
        public string AppLocation { get; set; }
        [JsonProperty]
        public string AppName { get; set; }
        [JsonProperty]
        public string WebAppID { get; set; }
        [JsonProperty]
        public string ConfiguredAppName { get; set; }
        [JsonProperty]
        public string ConfiguredEnvironmentName { get; set; }

        [JsonProperty]
        public bool IsAzureWorkerRole { get; set; }

        [JsonIgnore]
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
