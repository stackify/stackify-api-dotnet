using System;
using StackifyLib.Utils;

namespace StackifyLib
{
    public static class Extensions
    {
        public static void SendToStackify(this Exception ex)
        {
            try
            {
                StackifyLib.StackifyError.New(ex).SendToStackify();
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log("#Extensions #SendToStackify failed", e);
                throw;
            }
        }

        public static StackifyLib.StackifyError NewStackifyError(this Exception ex)
        {
            try
            {
                return StackifyLib.StackifyError.New(ex);
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log("#Extensions #NewStackifyError failed", e);
                throw;
            }
        }

#if NETCORE || NETCOREX
        public static void ConfigureStackifyLogging(this Microsoft.Extensions.Configuration.IConfigurationRoot configuration)
        {
            Config.SetConfiguration(configuration);
            //tell it to load all the settings since we now have the config
            Config.LoadSettings();
        }
#endif
    }
}