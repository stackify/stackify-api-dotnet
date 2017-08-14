using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using StackifyLib.Internal.Aws.Contract;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Aws
{
    internal class AwsEc2MetadataService : IAwsEc2MetadataService
    {
        // http://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-metadata.html#d0e30002
        protected const string EC2InstanceIdUrl = "http://169.254.169.254/latest/meta-data/instance-id";

        /// <summary>
        /// Get the EC2 Instance name if it exists else null
        /// </summary>
        public async Task<string> GetEC2InstanceIdAsync()
        {
            string r = null;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var content = await client.GetAsync(EC2InstanceIdUrl);

                    int statusCode = (int)content.StatusCode;

                    if (statusCode >= 200 && statusCode < 300)
                    {
                        string id = await content.Content.ReadAsStringAsync();
                        r = string.IsNullOrWhiteSpace(id) ? null : id;
                    }
                }
            }
            catch // if not in aws this will timeout
            {
                r = null;
            }

            StackifyAPILogger.Log($"AwsEc2 GetEC2InstanceIdAsync, value { r ?? "null" }");

            return r;
        }
    }
}
