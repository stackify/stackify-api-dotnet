using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using StackifyLib.Internal.Aws.Contract;
using StackifyLib.Internal.Scheduling;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Aws
{
    internal class AwsEc2MetadataService : IAwsEc2MetadataService
    {
        // http://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-metadata.html#d0e30002
        protected const string EC2InstanceIdUrl = "http://169.254.169.254/latest/meta-data/instance-id";
        public static readonly object ec2InstanceLock = new object();
        private string ec2InstanceId = null;

        private readonly IScheduler _scheduler;

        internal AwsEc2MetadataService(IScheduler scheduler, int refreshMinutes)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException("scheduler");

            if (_scheduler.IsStarted == false)
            {
                _scheduler.Schedule(OnTimerAsync, TimeSpan.FromMinutes(refreshMinutes));
            }
        }

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

            // SF-6804: Frequent Calls to GetEC2InstanceId
            lock (ec2InstanceLock)
            {
                ec2InstanceId = r;
            }

            return r;
        }

        private void OnTimerAsync(object state)
        {
            // SF-6804: Frequent Calls to GetEC2InstanceId
            try
            {
                GetEC2InstanceIdAsync().Wait();

                StackifyAPILogger.Log($"AwsEc2 OnTimerAsync, value { ec2InstanceId ?? "null" }");
            }
            catch (AggregateException aggEx)
            {
                foreach (var ex in aggEx.InnerExceptions)
                {
                    StackifyAPILogger.Log($"Failed to get ec2 instance-id due to aggregate exception. \r\n{ ex.Message }");
                }
            }
        }
    }
}
