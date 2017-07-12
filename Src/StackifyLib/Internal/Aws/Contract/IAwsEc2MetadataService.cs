using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace StackifyLib.Internal.Aws.Contract
{
    internal interface IAwsEc2MetadataService
    {
        Task<string> GetEC2InstanceIdAsync();
    }
}
