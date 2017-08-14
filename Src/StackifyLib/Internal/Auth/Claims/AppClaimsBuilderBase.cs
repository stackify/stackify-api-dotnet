using System;
using System.Threading.Tasks;
using StackifyLib.Internal.Aws.Contract;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Auth.Claims
{
    internal abstract class AppClaimsBuilderBase : IAppClaimsBuilder
    {
        protected readonly IAwsEc2MetadataService AwsMetadataService;
        protected readonly AppClaims AppClaims = new AppClaims();
        protected bool IsWebRequest = false;

        internal AppClaimsBuilderBase(IAwsEc2MetadataService aws)
        {
            AwsMetadataService = aws ?? throw new ArgumentNullException("aws");
        }

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

        protected abstract Task BuildClaimsAsync();
    }
}