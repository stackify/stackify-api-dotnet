using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StackifyLib.Utils
{
    internal static class DateExtensions
    {
        public static DateTime Round(this DateTime date, TimeSpan span)
        {
            long ticks = (date.Ticks + (span.Ticks / 2) + 1) / span.Ticks;
            return new DateTime(ticks * span.Ticks, date.Kind);
        }
        public static DateTime Floor(this DateTime date, TimeSpan span)
        {
            long ticks = (date.Ticks / span.Ticks);
            return new DateTime(ticks * span.Ticks, date.Kind);
        }
        public static DateTime Ceil(this DateTime date, TimeSpan span)
        {
            long ticks = (date.Ticks + span.Ticks - 1) / span.Ticks;
            return new DateTime(ticks * span.Ticks, date.Kind);
        }

        /// <summary>
        /// Calculates the Unix epoch for the date
        /// </summary>
        /// <returns>The Unix epoch</returns>
        public static long ToUnixEpoch(this DateTime date)
        {
            return (date.Ticks - 621355968000000000) / 10000000;
        }

        /// <summary>
        /// Calculates the Unix epoch minutes for the date
        /// </summary>
        /// <returns>The Unix epoch minutes</returns>
        public static long ToUnixEpochMinutes(this DateTime date)
        {
            return date.ToUnixEpoch() / 60;
        }
    }
}
