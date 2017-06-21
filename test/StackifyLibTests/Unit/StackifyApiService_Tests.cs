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
using System.Net;
using System.Net.Http;

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
        public async Task UploadAsync_Returns_401_If_Authentication_Fails() 
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
            Assert.Equal(401, (int)uploadResult);
            Assert.False(result);
        }

        [Fact]
        public async Task UploadAsync_Sets_LogMsg_Token_And_Returns_StatusCode_If_Successful() 
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
            
            var httpResonse = new HttpResponseMessage();
            httpResonse.StatusCode = HttpStatusCode.Accepted;
            _httpClientMock.Setup(m => m.PostAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<AccessTokenResponse>(), It.IsAny<bool>()))
                .ReturnsAsync(httpResonse);

            var service = new StackifyApiService(_tokenStoreMock.Object, _httpClientMock.Object);

            // act
            var uploadResult = await service.UploadAsync(claims, string.Empty, data);

            // assert
            Assert.Equal(httpResonse.StatusCode, uploadResult);
            Assert.Equal(data.AccessToken, accessToken.AccessToken);
        }

        [Fact]
        public async Task UploadAsync_Returns_0_If_Data_Is_Null() 
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
            Assert.Equal(0, (int)uploadResult);
        }
    }
}