#if NETSTANDARD1_3
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using StackifyLib.Internal.Aws.Contract;

namespace StackifyLib.Internal.Auth.Claims
{
    internal class AppClaimsBuilderStandard : AppClaimsBuilderBase
    {
        internal AppClaimsBuilderStandard(IAwsEc2MetadataService aws) : base(aws)
        {
        }

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
            AppClaims.DeviceName = await AwsMetadataService.GetEC2InstanceIdAsync()
                ?? Process.GetCurrentProcess().MachineName;
        }
    }
}
#endif