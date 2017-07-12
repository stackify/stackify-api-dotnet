using StackifyLib.Internal.Aws;
using StackifyLib.Internal.Scheduling;

namespace StackifyLib.Internal.Auth.Claims
{
    internal static class AppClaimsBuilderFactory
    {
        public static IAppClaimsBuilder Get()
        {
#if NET451 || NET45
            return new AppClaimsBuilderFullFramework(new AwsEc2MetadataService(new Scheduler(), Config.Ec2InstanceMetadataUpdateThresholdMinutes));
#else
            return new AppClaimsBuilderStandard(new AwsEc2MetadataService(new Scheduler(), Config.Ec2InstanceMetadataUpdateThresholdMinutes));
#endif
        }
    }
}
