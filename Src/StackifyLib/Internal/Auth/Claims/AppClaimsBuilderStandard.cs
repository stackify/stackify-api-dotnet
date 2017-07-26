#if NETSTANDARD1_3
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;

namespace StackifyLib.Internal.Auth.Claims
{
    internal class AppClaimsBuilderStandard : AppClaimsBuilderBase
    {
        protected override async Task BuildClaimsAsync()
        {
            // Logger global properties would override everything
            AppClaims.ConfiguredAppName = string.IsNullOrEmpty(Config.AppName) ? Config.Get("Stackify.AppName") : Config.AppName;
            AppClaims.ConfiguredEnvironmentName = string.IsNullOrEmpty(Config.Environment) ? Config.Get("Stackify.Environment") : Config.Environment;

            await SetDeviceName();

            AppClaims.AppLocation = AppContext.BaseDirectory;
        }

        private async Task SetDeviceName()
        {
            var machineName = Process.GetCurrentProcess().MachineName;

            if (Config.IsEc2 == true || (machineName.StartsWith("EC2") && machineName.Contains("-")))
            {
                AppClaims.DeviceName = await GetEC2InstanceId() ?? machineName;
            }
            else
            {
                AppClaims.DeviceName = machineName;
            }
        }

        /// <summary>
        /// Get the EC2 Instance name if it exists else null
        /// </summary>
        private static async Task<string> GetEC2InstanceId()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var content = await client.GetAsync(EC2InstanceIdUrl);

                    int statusCode = (int)content.StatusCode;

                    if (statusCode >= 200 && statusCode < 300)
                    {
                        string id = await content.Content.ReadAsStringAsync();
                        return string.IsNullOrWhiteSpace(id) ? null : id;
                    }
                }

            }
            catch // if not in aws this will timeout
            { }

            return null;
        }
    }
}
#endif