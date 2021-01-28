using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StackifyLib.UnitTests
{
    public class ConfigJSON_Tests
    {
        private readonly ITestOutputHelper output;
        private readonly string AppName = "TestAppName";
        private readonly string Environment = "TestAppName";
        private readonly string ApiKey = "TestAppName";

        public ConfigJSON_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }


        [Fact]
        public void Should_Ignore_Invalid_Format_Json_File()
        {
            ResetConfig();

            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(baseDirectory, "Fixtures", "JsonConfig", "Stackify-Invalid-Format.json");

            StackifyLib.Config.ReadStackifyJSONConfig(jsonPath);

            Assert.Equal(StackifyLib.Config.AppName, AppName);
            Assert.Equal(StackifyLib.Config.Environment, Environment);
            Assert.Equal(StackifyLib.Config.ApiKey, ApiKey);
        }

        [Fact]
        public void Should_Ignore_Invalid_Property_Type_Json_File()
        {
            ResetConfig();

            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(baseDirectory, "Fixtures", "JsonConfig", "Stackify-Invalid-Property-Type.json");

            StackifyLib.Config.ReadStackifyJSONConfig(jsonPath);

            Assert.Equal(StackifyLib.Config.AppName, AppName);
            Assert.Equal(StackifyLib.Config.Environment, Environment);
            Assert.Equal(StackifyLib.Config.ApiKey, ApiKey);
        }

        [Fact]
        public void Should_Ignore_Empty_Json_File()
        {
            ResetConfig();

            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(baseDirectory, "Fixtures", "JsonConfig", "Stackify-Empty.json");

            StackifyLib.Config.ReadStackifyJSONConfig(jsonPath);

            
            Assert.Equal(StackifyLib.Config.AppName, AppName);
            Assert.Equal(StackifyLib.Config.Environment, Environment);
            Assert.Equal(StackifyLib.Config.ApiKey, ApiKey);
        }

        [Fact]
        public void Should_Read_Valid_Format_Json_File()
        {
            /*
             {
              "AppName": "CoreConsoleApp",
              "Environment": "Dev",
              "ApiKey": "sampleKey"
            }
             */
            ResetConfig();

            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(baseDirectory, "Fixtures", "JsonConfig", "Stackify.json");

            StackifyLib.Config.ReadStackifyJSONConfig(jsonPath);

            Assert.Equal(StackifyLib.Config.AppName, "CoreConsoleApp");
            Assert.Equal(StackifyLib.Config.Environment, "Dev");
            Assert.Equal(StackifyLib.Config.ApiKey, "sampleKey");
        }

        [Fact]
        public void Should_Read_Valid_Format_With_Comment_Json_File()
        {
            /*
             {
              "AppName": "CoreConsoleApp",
              "Environment": "Dev",
              "ApiKey": "sampleKey"
            }
             */
            ResetConfig();

            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(baseDirectory, "Fixtures", "JsonConfig", "Stackify-With-Comment.json");

            StackifyLib.Config.ReadStackifyJSONConfig(jsonPath);

            Assert.Equal(StackifyLib.Config.AppName, "CoreConsoleApp");
            Assert.Equal(StackifyLib.Config.Environment, "Dev");
            Assert.Equal(StackifyLib.Config.ApiKey, "sampleKey");
        }

        private void ResetConfig()
        {
            StackifyLib.Config.AppName = AppName;
            StackifyLib.Config.Environment = Environment;
            StackifyLib.Config.ApiKey = ApiKey;
        }
    }
}
