using StackifyLib.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace StackifyLib.Utils
{
    public class ServerConfigHelper
    {
        public const string ProductionEnvName = "Production";

        public static string GetEnvironment()
        {
            if (AzureConfig.IsWebsite)
            {
                var key = Environment.GetEnvironmentVariable("Stackify.Environment");

                if (string.IsNullOrEmpty(key) == false)
                {
                    return key;
                }

                if (string.IsNullOrEmpty(AzureConfig.AzureAppWebConfigEnvironment) == false)
                {
                    return AzureConfig.AzureAppWebConfigEnvironment;
                }

                return ProductionEnvName;
            }

            return string.Empty;
        }
    }
}
