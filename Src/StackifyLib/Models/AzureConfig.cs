// Copyright (c) 2024-2025 BMC Software, Inc.
// Copyright (c) 2021-2024 Netreo
// Copyright (c) 2019 Stackify
using System;
using StackifyLib.Utils;
#if NETFULL
using Microsoft.Win32;
#endif

namespace StackifyLib.Models
{
    public class AzureConfig
    {
        private static AzureConfig _instance = new AzureConfig();
        public static AzureConfig Instance => _instance;

        private bool? _inAzure;
        private AzureRoleType _azureRoleType = AzureRoleType.Unknown;
        private string _azureRoleName;
        private string _azureInstanceName;
        private string _entryPoint;

        private readonly DateTime AppStarted = DateTime.UtcNow;

        private readonly object Locker = new object();
        public const string ProductionEnvName = "Production";


        public string AzureAppWebConfigEnvironment { get; set; }
        public string AzureAppWebConfigApiKey { get; set; }
        public string AzureDriveLetter { get; set; }

        public string AzureInstanceName
        {
            get
            {
                EnsureInAzureRan();
                return _azureInstanceName;
            }
        }

        public bool InAzure
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

        public bool IsWebsite
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

        private EnvironmentDetail _environmentDetail = null;

        public AzureConfig()
        {

        }

        public AzureConfig(EnvironmentDetail environmentDetail)
        {
            _environmentDetail = environmentDetail;
        }

        private void EnsureInAzureRan()
        {
            bool ensureTestHasBeenDone = InAzure;
        }

        public void LoadAzureSettings()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) == false && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")) == false)
            {
                _inAzure = true;
                _azureRoleType = AzureRoleType.WebApp;

                var slotId = GetDeploymentSlotId() ?? "0000";

                _azureRoleName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
                _azureInstanceName = $"{Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")} {GetEnvironment()} [{slotId}-{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID").Left(6)}]";

                return;
            }
        }

        public string GetDeploymentSlotId()
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

        public string GetEnvironment()
        {
            if (IsWebsite)
            {
                if (_environmentDetail == null)
                {
                    _environmentDetail = EnvironmentDetail.Get();
                }

                var key = Environment.GetEnvironmentVariable("Stackify.Environment");
                if (string.IsNullOrEmpty(key))
                {
                    key = _environmentDetail.ConfiguredEnvironmentName;
                }

                if (string.IsNullOrEmpty(key) == false)
                {
                    return key;
                }

                return ProductionEnvName;
            }

            return string.Empty;
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
