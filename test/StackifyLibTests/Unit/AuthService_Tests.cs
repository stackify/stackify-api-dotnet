using Xunit;
using Moq;
using StackifyLib.Http;
using StackifyLib.Auth;
using System.Threading.Tasks;
using System;
using StackifyLib.Internal.Auth.Claims;
using System.Collections.Generic;

namespace StackifyLibTests.Unit
{
    public class AuthService_Tests
    {
        private readonly Mock<IHttpRequestClient> _httpClientMock;

        public AuthService_Tests()
        {
            _httpClientMock = new Mock<IHttpRequestClient>();
        }

        [Fact]
        public async Task AuthService_Can_Get_Token() 
        {
            // arrange
            var claims = await AppClaimsManager.GetAsync();

            var expectedToken = new AccessTokenResponse {
                AccessToken = Guid.NewGuid().ToString()
            };

            _httpClientMock.Setup(m => m.PostAsync<AccessTokenResponse>(
                It.IsAny<string>(), 
                It.Is<Dictionary<string,string>>(d => ContainsRequiredKeys(d))))
                .ReturnsAsync(expectedToken);

            var authService = new AuthService(_httpClientMock.Object);

            // act
            var token = await authService.AuthenticateAsync(claims);

            // assert
            Assert.NotNull(token);
            Assert.Equal(expectedToken.AccessToken, token.AccessToken);
        }

        private bool ContainsRequiredKeys(Dictionary<string, string> dict)
        {
            return dict.ContainsKey("grant_type") &&
            dict.ContainsKey("scope") &&
            dict.ContainsKey("app_name") &&
            dict.ContainsKey("env_name") &&
            dict.ContainsKey("file_path") &&
            dict.ContainsKey("azure_worker_role") &&
            dict.ContainsKey("device_name") &&
            dict.ContainsKey("web_app_id") &&
            dict.ContainsKey("client_id");
        }
    }
}