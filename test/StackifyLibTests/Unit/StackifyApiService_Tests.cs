using Xunit;
using Moq;
using StackifyLib.Http;
using StackifyLib.Auth;
using System.Threading.Tasks;
using System;
using StackifyLib.Internal.Auth.Claims;
using System.Collections.Generic;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Models;

namespace StackifyLibTests.Unit
{
    public class StackifyApiService_Tests
    {
        private readonly Mock<IHttpRequestClient> _httpClientMock;
        private readonly Mock<ITokenStore> _tokenStoreMock;

        public StackifyApiService_Tests()
        {
            _httpClientMock = new Mock<IHttpRequestClient>();
            _tokenStoreMock = new Mock<ITokenStore>();
        }

        [Fact]
        public void CanUpload_Returns_True_On_First_Call() 
        {
            // arrange
            var service = new StackifyApiService(_tokenStoreMock.Object, _httpClientMock.Object);

            // act
            var result = service.CanUpload();

            // assert
            Assert.True(result);
        }

        [Fact]
        public async Task UploadAsync_Returns_False_If_Authentication_Fails() 
        {
            // arrange
            var claims = await AppClaimsManager.GetAsync();
            var data = new LogMsgGroup();
            _tokenStoreMock.Setup(m => 
                m.GetTokenAsync(It.Is<AppClaims>(c => c.Equals(claims))))
                .ThrowsAsync(new UnauthorizedAccessException());

            var service = new StackifyApiService(_tokenStoreMock.Object, _httpClientMock.Object);

            // act
            var uploadResult = await service.UploadAsync(claims, string.Empty, data);
            var result = service.CanUpload();

            // assert
            Assert.False(uploadResult);
            Assert.False(result);
        }

        [Fact]
        public async Task UploadAsync_Sets_LogMsg_Token_And_Returns_True_If_Successful() 
        {
            // arrange
            var claims = await AppClaimsManager.GetAsync();
            var data = new LogMsgGroup();
            var accessToken = new AccessTokenResponse {
                AccessToken = Guid.NewGuid().ToString()
            };
            _tokenStoreMock.Setup(m => 
                m.GetTokenAsync(It.Is<AppClaims>(c => c.Equals(claims))))
                .ReturnsAsync(accessToken);

            var service = new StackifyApiService(_tokenStoreMock.Object, _httpClientMock.Object);

            // act
            var uploadResult = await service.UploadAsync(claims, string.Empty, data);

            // assert
            Assert.True(uploadResult);
            Assert.Equal(data.AccessToken, accessToken.AccessToken);
        }

        [Fact]
        public async Task UploadAsync_ReturnsFalse_If_Data_Is_Null() 
        {
            // arrange
            var claims = await AppClaimsManager.GetAsync();
            var accessToken = new AccessTokenResponse {
                AccessToken = Guid.NewGuid().ToString()
            };
            _tokenStoreMock.Setup(m => 
                m.GetTokenAsync(It.Is<AppClaims>(c => c.Equals(claims))))
                .ReturnsAsync(accessToken);

            var service = new StackifyApiService(_tokenStoreMock.Object, _httpClientMock.Object);

            // act
            var uploadResult = await service.UploadAsync(claims, string.Empty, null);

            // assert
            Assert.False(uploadResult);
        }
    }
}