// Copyright (c) 2024-2025 BMC Software, Inc.
using StackifyLib.Models;
using System;
using System.Collections.Generic;
using Xunit;
using Moq;
#if NETCORE || NETCOREX
using Microsoft.Extensions.Configuration;
#endif

namespace StackifyLib.UnitTests.Models
{
    public class AzureConfig_Tests
    {
        private readonly MockRepository _mockRepository;
        private readonly Dictionary<string, string> _originalEnvironment = new Dictionary<string, string>();

        public AzureConfig_Tests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            BackupEnvironmentVariables();
        }

        public void Dispose()
        {
            RestoreEnvironmentVariables();
        }

        private void BackupEnvironmentVariables()
        {
            _originalEnvironment["WEBSITE_SITE_NAME"] = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            _originalEnvironment["WEBSITE_INSTANCE_ID"] = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            _originalEnvironment["WEBSITE_IIS_SITE_NAME"] = Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME");
            _originalEnvironment["Stackify.Environment"] = Environment.GetEnvironmentVariable("Stackify.Environment");
        }

        private void RestoreEnvironmentVariables()
        {
            foreach (var kvp in _originalEnvironment)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }

        private void ResetAzureConfig()
        {
            var instanceField = typeof(AzureConfig).GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            instanceField.SetValue(null, new AzureConfig());
#if NETCORE || NETCOREX
            Config.SetConfiguration(null);
#endif
        }

        private void SetEnvironmentVariables(string siteName, string instanceId, string iisSiteName = null)
        {
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", instanceId);
            Environment.SetEnvironmentVariable("WEBSITE_IIS_SITE_NAME", iisSiteName);
        }

        [Fact]
        public void InAzure_ReturnsTrue_WhenAzureEnvironmentDetected()
        {
            SetEnvironmentVariables("my-site", "instance-id");
            ResetAzureConfig();

            var result = new AzureConfig(new EnvironmentDetail()).InAzure;
            Assert.True(result);
        }

        [Fact]
        public void InAzure_ReturnsFalse_WhenNotInAzure()
        {
            SetEnvironmentVariables(null, null);
            ResetAzureConfig();

            var result = new AzureConfig(new EnvironmentDetail()).InAzure;
            Assert.False(result);
        }

        [Fact]
        public void IsWebsite_ReturnsTrue_WhenWebAppDetected()
        {
            SetEnvironmentVariables("my-site", "instance-id");
            ResetAzureConfig();

            var result = new AzureConfig(new EnvironmentDetail()).IsWebsite;
            Assert.True(result);
        }

        [Fact]
        public void AzureInstanceName_CorrectFormat_WhenInWebApp()
        {
            SetEnvironmentVariables("my-site", "full-instance-id-12345", "site__1234");
            ResetAzureConfig();

            var instanceName = new AzureConfig(new EnvironmentDetail()).AzureInstanceName;
            Assert.Equal("my-site Production [1234-full-i]", instanceName);
        }

        [Fact]
        public void GetDeploymentSlotId_ReturnsLast4Chars_WhenSlotPresent()
        {
            SetEnvironmentVariables(null, null, "site-name__abcd");
            ResetAzureConfig();

            var slotId = new AzureConfig(new EnvironmentDetail()).GetDeploymentSlotId();
            Assert.Equal("abcd", slotId);
        }

        [Fact]
        public void GetDeploymentSlotId_ReturnsNull_WhenNoSlotPresent()
        {
            SetEnvironmentVariables(null, null, "site-name");
            ResetAzureConfig();

            var slotId = new AzureConfig(new EnvironmentDetail()).GetDeploymentSlotId();
            Assert.Null(slotId);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void GetEnvironment_ReturnsConfiguredEnvironment_WhenSet()
        {
            SetEnvironmentVariables("site", "instance");
            Environment.SetEnvironmentVariable("Stackify.Environment", "Staging");
            ResetAzureConfig();

            var env = new AzureConfig(new EnvironmentDetail()).GetEnvironment();
            Assert.Equal("Staging", env);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void AzureInstanceName_CorrectFormat_WhenEnvStagingTestSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "StagingTest");
            SetEnvironmentVariables("my-site", "full-instance-id-12345", "site__1234");
            ResetAzureConfig();

            var instanceName = new AzureConfig(new EnvironmentDetail()).AzureInstanceName;
            Assert.Equal("my-site StagingTest [1234-full-i]", instanceName);
        }

        [Fact]
        public void GetEnvironment_ReturnsProduction_WhenNoConfigurationSet()
        {
            SetEnvironmentVariables("site", "instance");
            ResetAzureConfig();
            Config.LoadSettings();

            var env = new AzureConfig(new EnvironmentDetail()).GetEnvironment();
            Assert.Equal("Production", env);
        }

        [Fact]
        public void GetEnvironment_ReturnsEmptyString_WhenNotWebsite()
        {
            SetEnvironmentVariables(null, null); // Not in Azure
            ResetAzureConfig();

            var env = new AzureConfig().GetEnvironment();
            Assert.Equal(string.Empty, env);
        }

#if NETFULL
        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void GetEnvironment_ReturnsTest_WhenAppSettingsSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "");
            System.Configuration.ConfigurationManager.AppSettings["Stackify.Environment"] = "Test";
            SetEnvironmentVariables("site", "instance");
            ResetAzureConfig();

            var env = new AzureConfig(new EnvironmentDetail()).GetEnvironment();
            Assert.Equal("Test", env);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void GetEnvironment_ReturnsTest_WhenEnvSetFirst_AppSettingsSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "StagingFirstApp");
            System.Configuration.ConfigurationManager.AppSettings["Stackify.Environment"] = "Test";
            SetEnvironmentVariables("site", "instance");
            ResetAzureConfig();

            var env = new AzureConfig(new EnvironmentDetail()).GetEnvironment();
            Assert.Equal("StagingFirstApp", env);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void AzureInstanceName_CorrectFormat_WhenAppSettingsEnvStagingAppSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "");
            System.Configuration.ConfigurationManager.AppSettings["Stackify.Environment"] = "StagingApp";
            SetEnvironmentVariables("my-site", "full-instance-id-12345", "site__1234");
            ResetAzureConfig();

            var instanceName = new AzureConfig(new EnvironmentDetail()).AzureInstanceName;
            Assert.Equal("my-site StagingApp [1234-full-i]", instanceName);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void AzureInstanceName_CorrectFormat_WhenEnvSetFirst_AppSettings()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "StagingFirst");
            System.Configuration.ConfigurationManager.AppSettings["Stackify.Environment"] = "StagingApp";
            SetEnvironmentVariables("my-site", "full-instance-id-12345", "site__1234");
            ResetAzureConfig();

            var instanceName = new AzureConfig(new EnvironmentDetail()).AzureInstanceName;
            Assert.Equal("my-site StagingFirst [1234-full-i]", instanceName);
        }
#endif

#if NETCORE || NETCOREX
        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void GetEnvironment_ReturnsTestCore_WhenIConfigurationSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "");
            SetEnvironmentVariables("site", "instance");
            ResetAzureConfig();

            var iconfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Stackify:Environment"] = "TestCore"
                })
                .Build();
            Config.SetConfiguration(iconfig);
            Config.LoadSettings();

            var env = new AzureConfig(new EnvironmentDetail()).GetEnvironment();
            Assert.Equal("TestCore", env);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void GetEnvironment_ReturnsTestCore_WhenEnvFirst_IConfigurationSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "TestCoreFirst");
            SetEnvironmentVariables("site", "instance");
            ResetAzureConfig();

            var iconfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Stackify:Environment"] = "TestCore"
                })
                .Build();
            Config.SetConfiguration(iconfig);
            Config.LoadSettings();

            var env = new AzureConfig(new EnvironmentDetail()).GetEnvironment();
            Assert.Equal("TestCoreFirst", env);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void AzureInstanceName_CorrectFormat_WhenEnvSetFirst_IConfigurationEnvStagingConfigSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "StagingConfigFirst");
            SetEnvironmentVariables("my-site", "full-instance-id-12345", "site__1234");
            ResetAzureConfig();

            var iconfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Stackify:Environment"] = "StagingConfig"
                })
                .Build();
            Config.SetConfiguration(iconfig);
            Config.LoadSettings();

            var instanceName = new AzureConfig(new EnvironmentDetail()).AzureInstanceName;
            Assert.Equal("my-site StagingConfigFirst [1234-full-i]", instanceName);
        }

        [Fact(Skip = "Environment set is dynamic. Skip and run manually")]
        public void AzureInstanceName_CorrectFormat_WhenIConfigurationEnvStagingConfigSet()
        {
            Environment.SetEnvironmentVariable("Stackify.Environment", "");
            SetEnvironmentVariables("my-site", "full-instance-id-12345", "site__1234");
            ResetAzureConfig();

            var iconfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Stackify:Environment"] = "StagingConfig"
                })
                .Build();
            Config.SetConfiguration(iconfig);
            Config.LoadSettings();

            var instanceName = new AzureConfig(new EnvironmentDetail()).AzureInstanceName;
            Assert.Equal("my-site StagingConfig [1234-full-i]", instanceName);
        }
#endif

        [Fact]
        public void AzureRoleType_SetToWebApp_WhenWebsiteDetected()
        {
            // Arrange
            SetEnvironmentVariables("site", "instance");
            ResetAzureConfig();

            // Act
            bool _ = AzureConfig.Instance.InAzure; // Force initialization
            var roleType = GetPrivateField<AzureRoleType>("_azureRoleType");

            // Assert
            Assert.Equal(AzureRoleType.WebApp, roleType);
        }

        // Helper to access private fields
        private T GetPrivateField<T>(string fieldName)
        {
            var field = typeof(AzureConfig).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)field.GetValue(AzureConfig.Instance);
        }
    }
}
