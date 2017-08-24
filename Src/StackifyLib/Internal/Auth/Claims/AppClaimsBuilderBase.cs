using System;
using System.Threading.Tasks;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Auth.Claims
{
    internal abstract class AppClaimsBuilderBase : IAppClaimsBuilder
    {
        // http://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-metadata.html#d0e30002
        protected const string EC2InstanceIdUrl = "http://169.254.169.254/latest/meta-data/instance-id";
        protected readonly AppClaims AppClaims = new AppClaims();
        protected bool IsWebRequest = false;

        public async Task<AppClaims> Build()
        {
            try
            {
                await BuildClaimsAsync();
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error figuring out app environment details\r\n" + ex.ToString(), true);
            }

            return AppClaims;
        }

        protected async Task SetDeviceName()
        {
            var machineName = GetMachineName();

            if (Config.IsEc2 == null || Config.IsEc2 == true || (machineName.StartsWith("EC2") && machineName.Contains("-")))
            {
                AppClaims.DeviceName = await GetEC2InstanceId() ?? machineName;
            }
            else
            {
                AppClaims.DeviceName = machineName;
            }
        }

        protected abstract Task BuildClaimsAsync();
        protected abstract Task<string> GetEC2InstanceId();
        protected abstract string GetMachineName();
    }
}