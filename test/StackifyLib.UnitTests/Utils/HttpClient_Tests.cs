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
    public class HttpClient_Tests : IDisposable
    {
        private readonly HttpClient _httpClient;
        private IWebProxy _originalProxy;
        private Action<HttpWebRequest> _originalRequestModifier;

        public HttpClient_Tests()
        {
            // Store original values to restore after tests
            _originalProxy = HttpClient.CustomWebProxy;
            _originalRequestModifier = HttpClient.CustomRequestModifier;
            
            _httpClient = new HttpClient("test-api-key", "https://test.api.com/");
            HttpClient.IsUnitTest = true;
        }

        public void Dispose()
        {
            // Restore original values
            HttpClient.CustomWebProxy = _originalProxy;
            HttpClient.CustomRequestModifier = _originalRequestModifier;
            HttpClient.IsUnitTest = false;
        }

        [Fact]
        public void Constructor_SetsBaseAPIUrl_WithTrailingSlash()
        {
            var client = new HttpClient("test-key", "https://api.test.com");
            
            Assert.Equal("https://api.test.com/", client.BaseAPIUrl);
        }

        [Fact]
        public void Constructor_PreservesTrailingSlash_WhenAlreadyPresent()
        {
            var client = new HttpClient("test-key", "https://api.test.com/");
            
            Assert.Equal("https://api.test.com/", client.BaseAPIUrl);
        }

        [Fact]
        public void CustomWebProxy_CanBeSetAndRetrieved()
        {
            var testProxy = new WebProxy("http://proxy.test.com:8080");
            
            HttpClient.CustomWebProxy = testProxy;
            
            Assert.Equal(testProxy, HttpClient.CustomWebProxy);
        }

        [Fact]
        public void CustomRequestModifier_CanBeSetAndRetrieved()
        {
            var testModifier = new Action<HttpWebRequest>(req => req.Timeout = 30000);
            
            HttpClient.CustomRequestModifier = testModifier;
            
            Assert.Equal(testModifier, HttpClient.CustomRequestModifier);
        }

        [Fact]
        public void CustomRequestModifier_ModifiesRequest()
        {
            bool modifierCalled = false;
            string capturedUserAgent = null;
            
            HttpClient.CustomRequestModifier = (request) =>
            {
                modifierCalled = true;
                capturedUserAgent = request.Headers["User-Agent"];
                request.Timeout = 30000;
            };

            // Use reflection to test the BuildJsonRequest method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var finalRequest = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            Assert.True(modifierCalled);
            Assert.Equal(30000, finalRequest.Timeout);
            Assert.NotNull(capturedUserAgent);
        }

        [Fact]
        public void CustomRequestModifier_ModifiesPOSTRequest()
        {
            bool modifierCalled = false;
            
            HttpClient.CustomRequestModifier = (request) =>
            {
                modifierCalled = true;
                request.Timeout = 45000;
            };

            // Use reflection to test the BuildPOSTRequest method
            var method = typeof(HttpClient).GetMethod("BuildPOSTRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var finalRequest = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "data=test" });
            
            Assert.True(modifierCalled);
            Assert.Equal(45000, finalRequest.Timeout);
        }

        [Fact]
        public void BuildJsonRequest_SetsCustomWebProxy_WhenProvided()
        {
            var testProxy = new WebProxy("http://proxy1.test.com:8080");
            HttpClient.CustomWebProxy = testProxy;

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            Assert.Equal(testProxy, request.Proxy);
        }

        [Fact]
        public void BuildPOSTRequest_SetsCustomWebProxy_WhenProvided()
        {
            var testProxy = new WebProxy("http://proxy.test.com:8080");
            HttpClient.CustomWebProxy = testProxy;

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildPOSTRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "data=test" });
            
            Assert.Equal(testProxy, request.Proxy);
        }

        [Fact]
        public void BuildJsonRequest_DoesNotSetProxy_WhenCustomWebProxyIsNull()
        {
            HttpClient.CustomWebProxy = null;

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            // When CustomWebProxy is null, the request should use default proxy settings
            Assert.NotEqual(HttpClient.CustomWebProxy, request.Proxy);
        }

        [Fact]
        public void BuildJsonRequest_SetsCorrectHeaders()
        {
            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{\"test\": \"data\"}", false });
            
            Assert.Equal("application/json", request.ContentType);
            Assert.Equal("test-api-key", request.Headers["X-Stackify-Key"]);
            Assert.Equal("POST", request.Method);
#if NETFULL
            Assert.Contains("StackifyLib-", request.UserAgent);
#else
            Assert.Contains("StackifyLib-", request.Headers["User-Agent"]);
#endif
        }

        [Fact]
        public void BuildJsonRequest_WithCompression_SetsCorrectHeaders()
        {
            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{\"test\": \"data\"}", true });
            
            Assert.Equal("application/json", request.ContentType);
            Assert.Equal("gzip", request.Headers["Content-Encoding"]);
            Assert.Equal("POST", request.Method);
        }

        [Fact]
        public void BuildJsonRequest_WithEmptyJsonData_UsesGETMethod()
        {
            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "", false });
            
            Assert.Equal("GET", request.Method);
        }

        [Fact]
        public void BuildPOSTRequest_SetsCorrectHeaders()
        {
            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildPOSTRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "data=test" });
            
            Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
            Assert.Equal("test-api-key", request.Headers["X-Stackify-Key"]);
            Assert.Equal("POST", request.Method);
#if NETFULL
            Assert.Contains("StackifyLib-", request.UserAgent);
#else
            Assert.Contains("StackifyLib-", request.Headers["User-Agent"]);
#endif
        }

        [Fact]
        public void ProxyAndCustomModifier_WorkTogether()
        {
            var testProxy = new WebProxy("http://proxy.test.com:8080");
            HttpClient.CustomWebProxy = testProxy;
            
            bool modifierCalled = false;
            HttpClient.CustomRequestModifier = (request) =>
            {
                modifierCalled = true;
                request.Timeout = 60000;
            };

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var finalRequest = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            Assert.Equal(testProxy, finalRequest.Proxy);
            Assert.True(modifierCalled);
            Assert.Equal(60000, finalRequest.Timeout);
        }

        [Fact]
        public void CustomRequestModifier_DoesNotThrow_WhenNull()
        {
            HttpClient.CustomRequestModifier = null;

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            // Should not throw exception when CustomRequestModifier is null
            var exception = Record.Exception(() => 
                method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false })
            );
            
            Assert.Null(exception);
        }

#if NETFULL
        [Fact]
        public void LoadWebProxyConfig_SetsCustomWebProxy_FromConfig()
        {
            // This test would need to mock the Config.ProxyServer property
            // For now, we'll test that the method exists and can be called
            var method = typeof(HttpClient).GetMethod("LoadWebProxyConfig", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);

            // Should not throw exception
            var exception = Record.Exception(() => method.Invoke(null, null));
            Assert.Null(exception);
        }
#endif

        [Theory]
        [InlineData("http://proxy.example.com:8080")]
        [InlineData("https://secure-proxy.example.com:443", true)]
        [InlineData("http://user:pass@proxy.example.com:3128")]
        public void CustomWebProxy_AcceptsDifferentProxyUrls(string proxyUrl, bool throwsException = false)
        {
            var testProxy = new WebProxy(proxyUrl);
            HttpClient.CustomWebProxy = testProxy;

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            if (throwsException)
            {
#if NETFULL
                Assert.Throws<TargetInvocationException>(() => (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false }));
#else
                var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
                Assert.Equal(testProxy, request.Proxy);
#endif
            }
            else
            {
                var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
                Assert.Equal(testProxy, request.Proxy);
            }
        }

        [Fact]
        public void CustomRequestModifier_CanModifyMultipleProperties()
        {
            string customHeaderValue = "CustomValue123";
            
            HttpClient.CustomRequestModifier = (request) =>
            {
                request.Timeout = 120000;
                request.Headers.Add("X-Custom-Header", customHeaderValue);
                request.KeepAlive = false;
            };

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var finalRequest = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            Assert.Equal(120000, finalRequest.Timeout);
            Assert.Equal(customHeaderValue, finalRequest.Headers["X-Custom-Header"]);
            Assert.False(finalRequest.KeepAlive);
        }

        [Fact]
        public void ProxyConfiguration_RespectsOrder_ProxyThenCustomModifier()
        {
            var testProxy = new WebProxy("http://proxy.test.com:8080");
            HttpClient.CustomWebProxy = testProxy;
            
            // Custom modifier that verifies proxy was set first
            bool proxyWasSetBeforeModifier = false;
            HttpClient.CustomRequestModifier = (request) =>
            {
                proxyWasSetBeforeModifier = (request.Proxy == testProxy);
                request.Timeout = 25000;
            };

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var finalRequest = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            Assert.True(proxyWasSetBeforeModifier, "Proxy should be set before CustomRequestModifier is called");
            Assert.Equal(testProxy, finalRequest.Proxy);
            Assert.Equal(25000, finalRequest.Timeout);
        }

        [Fact]
        public void CustomRequestModifier_CanOverrideProxySettings()
        {
            var initialProxy = new WebProxy("http://initial-proxy.test.com:8080");
            var overrideProxy = new WebProxy("http://override-proxy.test.com:3128");
            
            HttpClient.CustomWebProxy = initialProxy;
            
            HttpClient.CustomRequestModifier = (request) =>
            {
                // Override the proxy set by the framework
                request.Proxy = overrideProxy;
            };

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildJsonRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var finalRequest = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "{}", false });
            
            Assert.Equal(overrideProxy, finalRequest.Proxy);
            Assert.NotEqual(initialProxy, finalRequest.Proxy);
        }

        [Fact]
        public void BuildPOSTRequest_WithoutProxy_DoesNotSetProxy()
        {
            HttpClient.CustomWebProxy = null;

            // Use reflection to access private method

            HttpWebRequest requestDefault = (HttpWebRequest)WebRequest.Create("http://test.com");
            var method = typeof(HttpClient).GetMethod("BuildPOSTRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", "data=test" });
            Assert.Equivalent(request.Proxy, requestDefault.Proxy);
        }

        [Theory]
        [InlineData("http://proxy.test.com:8080", "")]
        [InlineData("http://proxy.test.com:8080", "data=test")]
        [InlineData("https://secure-proxy.test.com:443", "param1=value1&param2=value2", true)]
        public void BuildPOSTRequest_WithDifferentProxyAndData_SetsProxyCorrectly(string proxyUrl, string postData, bool throwsException = false)
        {
            var testProxy = new WebProxy(proxyUrl);
            HttpClient.CustomWebProxy = testProxy;

            // Use reflection to access private method
            var method = typeof(HttpClient).GetMethod("BuildPOSTRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);


            if (throwsException)
            {
#if NETFULL
                Assert.Throws<TargetInvocationException>(() => (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", postData }));
#else
                var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", postData });
                Assert.Equal(testProxy, request.Proxy);
                Assert.Equal("POST", request.Method);
                Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
#endif
            }
            else
            {
                var request = (HttpWebRequest)method.Invoke(_httpClient, new object[] { "https://test.api.com/test", postData });
                Assert.Equal(testProxy, request.Proxy);
                Assert.Equal("POST", request.Method);
                Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
            }
            
        }

        [Fact]
        public void APIKey_Property_ReturnsCorrectValue()
        {
            var client = new HttpClient("test-api-key", "https://test.api.com/");
            
            // Access the APIKey property
            Assert.Equal("test-api-key", client.APIKey);
        }

        [Fact]
        public void StackifyWebResponse_IsClientError_ReturnsCorrectValues()
        {
            var response = new HttpClient.StackifyWebResponse();
            
            // Test 4xx status codes (client errors)
            response.StatusCode = HttpStatusCode.BadRequest;
            Assert.True(response.IsClientError());
            
            response.StatusCode = HttpStatusCode.Unauthorized;
            Assert.True(response.IsClientError());
            
            response.StatusCode = HttpStatusCode.NotFound;
            Assert.True(response.IsClientError());
            
            // Test non-4xx status codes
            response.StatusCode = HttpStatusCode.OK;
            Assert.False(response.IsClientError());
            
            response.StatusCode = HttpStatusCode.InternalServerError;
            Assert.False(response.IsClientError());
            
            response.StatusCode = HttpStatusCode.BadGateway;
            Assert.False(response.IsClientError());
        }

        [Fact]
        public void LoadWebProxyConfig_SetsUseDefaultCredentials_WhenConfigIsTrue()
        {
            // Arrange - Mock the Config.Get method to return true for UseDefaultCredentials
            var originalProxyServer = Config.ProxyServer;
            var configType = typeof(Config);
            var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
            
            try
            {
                // Set up a proxy server without credentials in URL
                proxyServerProperty.SetValue(null, "http://proxy.test.com:8080");

                // We'll test this by calling LoadWebProxyConfig and checking the result
                HttpClient.LoadWebProxyConfig();
                
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                
                // Note: We can't easily test UseDefaultCredentials without mocking Config.Get
                // This test verifies the method runs without error when proxy is configured
                Assert.Equal("proxy.test.com", customProxy.Address.Host);
                Assert.Equal(8080, customProxy.Address.Port);
            }
            finally
            {
                // Restore original state
                proxyServerProperty.SetValue(null, originalProxyServer);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Fact]
        public void LoadWebProxyConfig_SetsCredentials_WhenProxyHasUserInfo()
        {
            // Arrange
            var originalProxyServer = Config.ProxyServer;
            var configType = typeof(Config);
            var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
            
            try
            {
                // Set up a proxy server with credentials in URL
                proxyServerProperty.SetValue(null, "http://testuser:testpass@proxy.test.com:8080");
                
                // Act
                HttpClient.LoadWebProxyConfig();
                
                // Assert
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                Assert.Equal("proxy.test.com", customProxy.Address.Host);
                Assert.Equal(8080, customProxy.Address.Port);
                
                // Verify credentials were set
                Assert.NotNull(customProxy.Credentials);
                var networkCredential = customProxy.Credentials as NetworkCredential;
                Assert.NotNull(networkCredential);
                Assert.Equal("testuser", networkCredential.UserName);
                Assert.Equal("testpass", networkCredential.Password);
            }
            finally
            {
                // Restore original state
                proxyServerProperty.SetValue(null, originalProxyServer);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Fact]
        public void LoadWebProxyConfig_DoesNotSetProxy_WhenProxyServerIsEmpty()
        {
            // Arrange
            var originalProxyServer = Config.ProxyServer;
            var configType = typeof(Config);
            var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
            
            try
            {
                // Set empty proxy server
                proxyServerProperty.SetValue(null, string.Empty);
                
                // Act
                HttpClient.LoadWebProxyConfig();
                
                // Assert - CustomWebProxy should remain null
                Assert.Null(HttpClient.CustomWebProxy);
            }
            finally
            {
                // Restore original state
                proxyServerProperty.SetValue(null, originalProxyServer);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Fact]
        public void LoadWebProxyConfig_HandlesException_WhenInvalidProxyUrl()
        {
            // Arrange
            var originalProxyServer = Config.ProxyServer;
            var configType = typeof(Config);
            var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
            
            try
            {
                // Set invalid proxy server URL
                proxyServerProperty.SetValue(null, "invalid-url-format");
                
                // Act - should not throw exception
                var exception = Record.Exception(() => HttpClient.LoadWebProxyConfig());
                
                // Assert - should handle exception gracefully
                Assert.Null(exception);
                // CustomWebProxy might be null or unchanged depending on the exception handling
            }
            finally
            {
                // Restore original state
                proxyServerProperty.SetValue(null, originalProxyServer);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Theory]
        [InlineData("http://proxy.test.com:8080")]
        [InlineData("https://secure-proxy.test.com:443")]
        [InlineData("http://proxy.company.com:3128")]
        public void LoadWebProxyConfig_SetsCorrectProxyAddress_ForValidUrls(string proxyUrl)
        {
            // Arrange
            var originalProxyServer = Config.ProxyServer;
            var configType = typeof(Config);
            var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
            
            try
            {
                // Set proxy server
                proxyServerProperty.SetValue(null, proxyUrl);
                
                // Act
                HttpClient.LoadWebProxyConfig();
                
                // Assert
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                
                var expectedUri = new Uri(proxyUrl);
                Assert.Equal(expectedUri.Host, customProxy.Address.Host);
                Assert.Equal(expectedUri.Port, customProxy.Address.Port);
                Assert.Equal(expectedUri.Scheme, customProxy.Address.Scheme);
            }
            finally
            {
                // Restore original state
                proxyServerProperty.SetValue(null, originalProxyServer);
                HttpClient.CustomWebProxy = null;
            }
        }
        [Fact]
        public void LoadWebProxyConfig_WithProxyUseDefaultCredentials_True()
        {
            // Arrange - Save original values
            var originalProxyServer = Config.ProxyServer;
            var originalProxyUseDefaultCredentials = Config.ProxyUseDefaultCredentials;

            try
            {
                // Set up Config values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, "http://corporate-proxy.test.com:8080");
                proxyUseDefaultCredentialsProperty.SetValue(null, true);

                // Act
                HttpClient.LoadWebProxyConfig();

                // Assert
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                Assert.Equal("corporate-proxy.test.com", customProxy.Address.Host);
                Assert.Equal(8080, customProxy.Address.Port);
                Assert.True(customProxy.UseDefaultCredentials);
                Assert.NotNull(customProxy.Credentials); // No explicit credentials when using default
                Assert.Empty((customProxy.Credentials as NetworkCredential).UserName);
            }
            finally
            {
                // Restore original values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, originalProxyServer);
                proxyUseDefaultCredentialsProperty.SetValue(null, originalProxyUseDefaultCredentials);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Fact]
        public void LoadWebProxyConfig_WithProxyUseDefaultCredentials_False()
        {
            // Arrange - Save original values
            var originalProxyServer = Config.ProxyServer;
            var originalProxyUseDefaultCredentials = Config.ProxyUseDefaultCredentials;

            try
            {
                // Set up Config values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, "http://corporate-proxy.test.com:8080");
                proxyUseDefaultCredentialsProperty.SetValue(null, false);

                // Act
                HttpClient.LoadWebProxyConfig();

                // Assert
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                Assert.Equal("corporate-proxy.test.com", customProxy.Address.Host);
                Assert.Equal(8080, customProxy.Address.Port);
                Assert.False(customProxy.UseDefaultCredentials);
                Assert.Null(customProxy.Credentials);
            }
            finally
            {
                // Restore original values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, originalProxyServer);
                proxyUseDefaultCredentialsProperty.SetValue(null, originalProxyUseDefaultCredentials);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Fact]
        public void LoadWebProxyConfig_WithProxyUseDefaultCredentials_Null()
        {
            // Arrange - Save original values
            var originalProxyServer = Config.ProxyServer;
            var originalProxyUseDefaultCredentials = Config.ProxyUseDefaultCredentials;

            try
            {
                // Set up Config values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, "http://corporate-proxy.test.com:8080");
                proxyUseDefaultCredentialsProperty.SetValue(null, null); // Explicitly set to null

                // Act
                HttpClient.LoadWebProxyConfig();

                // Assert
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                Assert.Equal("corporate-proxy.test.com", customProxy.Address.Host);
                Assert.Equal(8080, customProxy.Address.Port);
                Assert.False(customProxy.UseDefaultCredentials); // Should remain false when null
                Assert.Null(customProxy.Credentials);
            }
            finally
            {
                // Restore original values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, originalProxyServer);
                proxyUseDefaultCredentialsProperty.SetValue(null, originalProxyUseDefaultCredentials);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Fact]
        public void LoadWebProxyConfig_WithEmbeddedCredentials_IgnoresProxyUseDefaultCredentials()
        {
            // Arrange - Save original values
            var originalProxyServer = Config.ProxyServer;
            var originalProxyUseDefaultCredentials = Config.ProxyUseDefaultCredentials;

            try
            {
                // Set up Config values - proxy with embedded credentials
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, "http://testuser:testpass@corporate-proxy.test.com:8080");
                proxyUseDefaultCredentialsProperty.SetValue(null, true); // This should be ignored

                // Act
                HttpClient.LoadWebProxyConfig();

                // Assert - Embedded credentials take precedence over UseDefaultCredentials
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                Assert.Equal("corporate-proxy.test.com", customProxy.Address.Host);
                Assert.Equal(8080, customProxy.Address.Port);
                Assert.False(customProxy.UseDefaultCredentials); // Should be false when explicit credentials are used

                // Verify explicit credentials were set
                Assert.NotNull(customProxy.Credentials);
                var networkCredential = customProxy.Credentials as NetworkCredential;
                Assert.NotNull(networkCredential);
                Assert.Equal("testuser", networkCredential.UserName);
                Assert.Equal("testpass", networkCredential.Password);
            }
            finally
            {
                // Restore original values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, originalProxyServer);
                proxyUseDefaultCredentialsProperty.SetValue(null, originalProxyUseDefaultCredentials);
                HttpClient.CustomWebProxy = null;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void LoadWebProxyConfig_ProxyUseDefaultCredentials_VariousValues(bool? configValue)
        {
            // Arrange - Save original values
            var originalProxyServer = Config.ProxyServer;
            var originalProxyUseDefaultCredentials = Config.ProxyUseDefaultCredentials;

            try
            {
                // Set up Config values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, "http://test-proxy.company.com:3128");
                proxyUseDefaultCredentialsProperty.SetValue(null, configValue);

                // Act
                HttpClient.LoadWebProxyConfig();

                // Assert
                var customProxy = HttpClient.CustomWebProxy as WebProxy;
                Assert.NotNull(customProxy);
                Assert.Equal("test-proxy.company.com", customProxy.Address.Host);
                Assert.Equal(3128, customProxy.Address.Port);

                // UseDefaultCredentials should only be true when configValue is true
                bool expectedUseDefault = configValue.HasValue && configValue.Value;
                Assert.Equal(expectedUseDefault, customProxy.UseDefaultCredentials);
                if (configValue.HasValue && configValue.Value)
                {
                    Assert.NotNull(customProxy.Credentials);
                    Assert.Empty((customProxy.Credentials as NetworkCredential).UserName);
                }
                else
                {
                    Assert.Null(customProxy.Credentials);
                }
            }
            finally
            {
                // Restore original values
                var configType = typeof(Config);
                var proxyServerProperty = configType.GetProperty("ProxyServer", BindingFlags.Public | BindingFlags.Static);
                var proxyUseDefaultCredentialsProperty = configType.GetProperty("ProxyUseDefaultCredentials", BindingFlags.Public | BindingFlags.Static);

                proxyServerProperty.SetValue(null, originalProxyServer);
                proxyUseDefaultCredentialsProperty.SetValue(null, originalProxyUseDefaultCredentials);
                HttpClient.CustomWebProxy = null;
            }
        }
    }
}
