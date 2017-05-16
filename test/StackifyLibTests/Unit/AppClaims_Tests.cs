using StackifyLib.Auth;
using StackifyLib.Internal.Auth.Claims;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class AppClaims_Tests
    {
        [Fact]
        public void Equals_Returns_True_If_All_Fields_Are_Identical() 
        {
            // arrange
            var claims1 = GetAppClaims(1);
            var claims2 = GetAppClaims(1);

            // act
            var result = claims1.Equals(claims2);

            // assert
            Assert.True(result);
        }

        [Fact]
        public void Equals_Returns_False_If_All_Fields_Are_Not_Identical() 
        {
            // arrange
            var claims1 = GetAppClaims(1);
            var claims2 = GetAppClaims(2);

            // act
            var result = claims1.Equals(claims2);

            // assert
            Assert.False(result);
        }

        [Fact]
        public void GetHashCode_Is_Equal_For_Identical_Instances() 
        {
            // arrange
            var claims1 = GetAppClaims(1);
            var claims2 = GetAppClaims(1);

            // act
            var result1 = claims1.GetHashCode();
            var result2 = claims2.GetHashCode();

            // assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void GetHashCode_Is_Not_Equal_For_Different_Instances() 
        {
            // arrange
            var claims1 = GetAppClaims(1);
            var claims2 = GetAppClaims(2);

            // act
            var result1 = claims1.GetHashCode();
            var result2 = claims2.GetHashCode();

            // assert
            Assert.NotEqual(result1, result2);
        }

        private AppClaims GetAppClaims(int instanceId)
        {
            return new AppClaims {
                DeviceName = "DeviceName" + instanceId,
                AppLocation = "AppLocation" + instanceId,
                AppName = "AppName" + instanceId,
                WebAppID = "WebAppID" + instanceId,
                ConfiguredAppName = "ConfiguredAppName" + instanceId,
                ConfiguredEnvironmentName = "ConfiguredEnvironmentName" + instanceId,
                IsAzureWorkerRole = false,
                AzureInstanceName = "AzureInstanceName" + instanceId
            };
        }
    }
}