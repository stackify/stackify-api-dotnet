// Copyright (c) 2024 BMC Software, Inc.
// Copyright (c) 2021-2024 Netreo
// Copyright (c) 2019 Stackify
using StackifyLib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace StackifyLib.UnitTests.Models
{
    public class EnvironmentDetail_Tests
    {
#if NETCORE
        [Fact]
        public async Task TestIfConfigEc2IsNull()
        {
            var envService = new EnvironmentDetail();
            Assert.Equal(Environment.MachineName, envService.GetDeviceName());

            var type = envService.GetType();
            var property = type.GetField("ec2InstanceId", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Equal(property.GetValue(envService) as string, string.Empty);
        }
#endif
#if NETFULL
        [Fact]
        public void TestIfConfigEc2IsNull()
        {
            var envService = new EnvironmentDetail();
            Assert.Equal(Environment.MachineName, envService.GetDeviceName());

            var type = envService.GetType();
            var property = type.GetField("ec2InstanceId", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Equal(property.GetValue(envService) as string, string.Empty);
        }
#endif
    }
}
