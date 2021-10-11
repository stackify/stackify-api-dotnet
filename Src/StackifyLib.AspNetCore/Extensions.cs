using System;
using System.Diagnostics;
using StackifyLib.AspNetCore;

namespace StackifyLib
{
    public static class Extensions
    {
        public static void ConfigureStackifyLogging(this Microsoft.AspNetCore.Builder.IApplicationBuilder app, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            try
            {
                Configure.SubscribeToWebRequestDetails(app.ApplicationServices);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"#Extensions - #AspNetCore - Error Subscribing to WebRequestDetails {ex}");
                StackifyLib.Utils.StackifyAPILogger.Log("#Extensions - #AspNetCore - Error Subscribing to WebRequestDetails", ex);
            }

            try
            {
                if (configuration == null)
                {
                    StackifyLib.Utils.StackifyAPILogger.Log("#Extensions - #AspNetCore - Empty configuration, ignoring");
                    return;
                }

                StackifyLib.Utils.StackifyAPILogger.Log("#Extensions - #AspNetCore - ConfigureStackifyLogging - Initialize");
                StackifyLib.Config.SetConfiguration(configuration);
                StackifyLib.Utils.StackifyAPILogger.Log("#Extensions - #AspNetCore - ConfigureStackifyLogging - Settings Loaded");
                Config.LoadSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"#Extensions - #AspNetCore - Error in ConfigureStackifyLogging {ex}");
                StackifyLib.Utils.StackifyAPILogger.Log("#Extensions - #AspNetCore - Error in ConfigureStackifyLogging", ex);
            }
        }
    }
}