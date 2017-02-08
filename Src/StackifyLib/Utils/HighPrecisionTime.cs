using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace StackifyLib.Utils
{
    public static class HighPrecisionTime
    {
        public static bool IsAvailable { get; private set; }

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        public static DateTime UtcNow
        {
            get
            {
                if (!IsAvailable)
                {
                    return DateTime.UtcNow;
                    // throw new InvalidOperationException("High resolution clock isn't available.");
                }
                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);
                return DateTime.FromFileTimeUtc(filetime);
            }
        }

        static HighPrecisionTime()
        {
            try
            {
                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);
                IsAvailable = true;
            }
            catch 
            {
                 IsAvailable = false;       
            }
        }
    }
}
