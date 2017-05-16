using StackifyLib.Auth;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class AccesTokenResponse_Tests
    {
        [Fact]
        public void IsExpired_Returns_False_If_Not_Expired() 
        {
            // arrange
            var token = new AccessTokenResponse {
                ExpiresIn = 10000
            };

            // act
            var result = token.IsExpired();

            // assert
            Assert.False(result);
        }

         [Fact]
        public void IsExpired_Returns_True_If_Expired() 
        {
            // arrange
            var token = new AccessTokenResponse {
                ExpiresIn = 0
            };

            // act
            var result = token.IsExpired();

            // assert
            Assert.True(result);
        }
    }
}