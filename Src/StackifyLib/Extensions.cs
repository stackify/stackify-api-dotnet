using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Debug.WriteLine("Error submitting error to Stackify " + e.ToString());
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
                Debug.WriteLine("Error submitting error to Stackify " + e.ToString());
                throw;
            }
        }

#if NETSTANDARD1_3
        public static void ConfigureStackifyLogging(this Microsoft.Extensions.Configuration.IConfigurationRoot configuration)
        {
            Config.SetConfiguration(configuration);
            //tell it to load all the settings since we now have the config
            Config.LoadSettings();
        }
#endif
    }
}
