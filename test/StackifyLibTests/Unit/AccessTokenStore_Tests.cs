using System;
using System.Threading.Tasks;
using Moq;
using StackifyLib.Auth;
using StackifyLib.Internal.Auth.Claims;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class AccessTokenStore_Tests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        public AccessTokenStore_Tests()
        {
            _authServiceMock = new Mock<IAuthService>();
        }

        [Fact]
        public async Task GetTokenAsync_Requests_TokenAsync()
        {
            // arrange
            var claims = new AppClaims();
            var store = new AccessTokenStore(_authServiceMock.Object);

            // act
            var result = await store.GetTokenAsync(claims);

            // assert
            _authServiceMock.Verify(m => m.AuthenticateAsync(It.Is<AppClaims>(c => c == claims)), Times.Once);
        }

        [Fact]
        public async Task GetTokenAsync_Requests_Tokens_For_Multiple_AppsAsync()
        {
            // arrange
            var app1 = new AppClaims
            {
                AppName = "app1"
            };
            var app2 = new AppClaims
            {
                AppName = "app2"
            };

            var store = new AccessTokenStore(_authServiceMock.Object);

            // act
            await store.GetTokenAsync(app1);
            await store.GetTokenAsync(app2);

            // assert
            _authServiceMock.Verify(m => m.AuthenticateAsync(It.IsAny<AppClaims>()), Times.Exactly(2));
        }

        [Fact]
        public async Task GetTokenAsync_Doesnt_Request_New_Token_For_Known_App()
        {
            // arrange
            var app1 = new AppClaims
            {
                AppName = "app1"
            };

            var tokenResponse = new AccessTokenResponse
            {
                AccessToken = Guid.NewGuid().ToString(),
                ExpiresIn = 1000
            };
            _authServiceMock
            .Setup(m => m.AuthenticateAsync(It.Is<AppClaims>(c => c == app1)))
                .ReturnsAsync(tokenResponse);

            var store = new AccessTokenStore(_authServiceMock.Object);

            // act
            await store.GetTokenAsync(app1);
            await store.GetTokenAsync(app1);

            // assert
            _authServiceMock.Verify(m => m.AuthenticateAsync(It.IsAny<AppClaims>()), Times.Once);
        }

        [Fact]
        public async Task GetTokenAsync_Requests_New_Token_If_Token_IsExpired()
        {
            // arrange
            var app1 = new AppClaims
            {
                AppName = "app1"
            };

            var tokenResponse = new AccessTokenResponse
            {
                AccessToken = Guid.NewGuid().ToString(),
                ExpiresIn = 0
            };
            _authServiceMock
            .Setup(m => m.AuthenticateAsync(It.Is<AppClaims>(c => c == app1)))
                .ReturnsAsync(tokenResponse);

            var store = new AccessTokenStore(_authServiceMock.Object);

            // act
            var response1 = await store.GetTokenAsync(app1);
            var response2 = await store.GetTokenAsync(app1);

            // assert
            _authServiceMock.Verify(m => m.AuthenticateAsync(It.IsAny<AppClaims>()), Times.Exactly(2));
            Assert.Equal(tokenResponse.AccessToken, response1.AccessToken);
            Assert.Equal(tokenResponse.AccessToken, response2.AccessToken);
        }
    }
}