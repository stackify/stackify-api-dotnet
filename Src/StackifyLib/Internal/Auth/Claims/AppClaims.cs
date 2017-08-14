using System;
using Newtonsoft.Json;

namespace StackifyLib.Internal.Auth.Claims
{
    public class AppClaims
    {
        public AppClaims() { }

        public string DeviceName { get; set; }
        public string AppLocation { get; set; }
        public string AppName { get; set; }
        public string WebAppID { get; set; }
        public string ConfiguredAppName { get; set; }
        public string ConfiguredEnvironmentName { get; set; }
        public bool IsAzureWorkerRole { get; set; }
        public string Platform { get; set; } = ".net";

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

        #region Equality Checks
        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;

            var c = obj as AppClaims;
            if ((Object)c == null)
                return false;

            return (DeviceName == c.DeviceName)
                && (AppLocation == c.AppLocation)
                && (AppName == c.AppName)
                && (WebAppID == c.WebAppID)
                && (ConfiguredAppName == c.ConfiguredAppName)
                && (ConfiguredEnvironmentName == c.ConfiguredEnvironmentName)
                && (IsAzureWorkerRole == c.IsAzureWorkerRole)
                && (Platform == c.Platform);
        }

        // see Jon Skeet's not-quite-FNV algorithm
        public override int GetHashCode()
        {
             // overflow is fine, just wrap
            unchecked
            {
                const int multiple = 16777619;
                int hash = (int)2166136261;
                
                hash = (hash * multiple) ^ DeviceName?.GetHashCode() ?? 0;
                hash = (hash * multiple) ^ AppLocation?.GetHashCode() ?? 0;
                hash = (hash * multiple) ^ AppName?.GetHashCode() ?? 0;
                hash = (hash * multiple) ^ WebAppID?.GetHashCode() ?? 0;
                hash = (hash * multiple) ^ ConfiguredAppName?.GetHashCode() ?? 0;
                hash = (hash * multiple) ^ ConfiguredEnvironmentName?.GetHashCode() ?? 0;
                hash = (hash * multiple) ^ IsAzureWorkerRole.GetHashCode();
                hash = (hash * multiple) ^ Platform.GetHashCode();
                hash = (hash * multiple) ^ AzureInstanceName?.GetHashCode() ?? 0;
                return hash;
            }
        }
        #endregion
    }
}
