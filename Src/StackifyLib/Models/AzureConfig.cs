using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using StackifyLib.Utils;
using System.Linq;
#if NETFULL
using Microsoft.Win32;
#endif

namespace StackifyLib.Models
{
    public class AzureConfig
    {
        private static bool? _inAzure;
        private static AzureRoleType _azureRoleType = AzureRoleType.Unknown;
        private static string _azureRoleName;
        private static string _azureInstanceName;
        private static string _entryPoint;

        private static readonly DateTime AppStarted = DateTime.UtcNow;

        private static readonly object Locker = new object();


        public static string AzureAppWebConfigEnvironment { get; set; }
        public static string AzureAppWebConfigApiKey { get; set; }
        public static string AzureDriveLetter { get; set; }

        public static string AzureInstanceName
        {
            get
            {
                EnsureInAzureRan();
                return _azureInstanceName;
            }
        }

        public static bool InAzure
        {
            get
            {
                if (_inAzure == null)
                {
                    lock (Locker)
                    {
                        try
                        {
                            _inAzure = false;

                            LoadAzureSettings();
                        }
                        catch (Exception ex)
                        {
                            StackifyLib.Utils.StackifyAPILogger.Log("Unable to load azure runtime", ex);
                        }
                    }
                }

                return _inAzure ?? false;
            }
        }

        public static bool IsWebsite
        {
            get
            {
                if (InAzure)
                {
                    return _azureRoleType == AzureRoleType.WebApp;
                }

                return false;
            }
        }

        private static void EnsureInAzureRan()
        {
            bool ensureTestHasBeenDone = InAzure;
        }

        public static void LoadAzureSettings()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) == false && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")) == false)
            {
                _inAzure = true;
                _azureRoleType = AzureRoleType.WebApp;

                var slotId = GetDeploymentSlotId() ?? "0000";

                _azureRoleName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
                _azureInstanceName = $"{Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")} {ServerConfigHelper.GetEnvironment()} [{slotId}-{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID").Left(6)}]";

                return;
            }
        }

        public static string GetDeploymentSlotId()
        {
            try
            {
                var siteName2 = Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME") ?? string.Empty;
                if (siteName2.Contains("__"))
                {
                    return siteName2.Right(4);
                }
            }
            catch (Exception ex)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("#Azure #GetDeploymentSlotId failed", ex);
            }

            return null;
        }
    }

    public enum AzureRoleType : short
    {
        Unknown = 0,
        NotAzure = 1,
        Web = 2,
        Worker = 3,
        Cache = 4,
        WebApp = 5
    }
}
