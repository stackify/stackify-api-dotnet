// Copyright (c) 2024-2025 BMC Software, Inc.
// Copyright (c) 2021-2024 Netreo
// Copyright (c) 2019 Stackify
using Moq;
using StackifyLib.Models;
using StackifyLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace StackifyLib.UnitTests.Utils
{
    public class HttpClient_Loading_Tests : IDisposable
    {
        private readonly HttpClient _httpClient;

        public HttpClient_Loading_Tests()
        {
            // Store original values to restore after tests
            Config.ProxyServer = "http://testuser:testpass@docker-hyperv.local:8881";
            _httpClient = new HttpClient("test-api-key", "https://test.api.com/");
            HttpClient.IsUnitTest = true;
        }

        public void Dispose()
        {
            HttpClient.IsUnitTest = false;
        }

        [Fact(Skip = "This test should be run explicitly (due to static loading nature of HttpClient)")]
        public void LoadWebProxyConfig_DefaultBehavior()
        {
            // Arrange - Mock the Config.Get method to return true for UseDefaultCredentials
            try
            {
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);

                // Note: We can't easily test UseDefaultCredentials without mocking Config.Get
                // This test verifies the method runs without error when proxy is configured
                Assert.Equal("docker-hyperv.local", customProxy.Address.Host);
                Assert.Equal(8881, customProxy.Address.Port);
            }
            finally
            {
            }
        }
    }
}
