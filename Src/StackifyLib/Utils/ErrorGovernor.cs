using StackifyLib.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StackifyLib.Utils
{   
    /// <summary>
    /// Handles error throttling from the client side appender
    /// </summary>
    public class ErrorGovernor
    {
        /// <summary>
        /// Number of instances of a unique error that are allowed to be sent in one minute 
        /// </summary>
        public static int MaxDupErrorPerMinute = 100;

        /// <summary>
        /// Elapsed time before the errorToCounter dictionary is purged of expired entries 
        /// </summary>
        private static readonly int cleanUpMinutes = 5;

        /// <summary>
        /// Dictionary from
        ///     MD5(<type>-<typeCode>-<method>)
        /// to
        ///     Unix epoch minute, error count for that minute
        /// </summary>
        private readonly Dictionary<string, Tuple<long, int>> errorToCounter = new Dictionary<string, Tuple<long, int>>();

        /// <summary>
        /// The next time the errorToCounter dictionary needs to be purged of expired entries
        /// </summary>
        private long nextErrorToCounterCleanUp = DateTime.UtcNow.ToUnixEpochMinutes() + cleanUpMinutes;

        /// <summary>
        /// Determines if the error should be sent based on our throttling criteria
        /// </summary>
        /// <param name="error">The error</param>
        /// <returns>True if this error should be sent to Stackify, false otherwise</returns>
        public bool ErrorShouldBeSent(StackifyError error)
        {
            if (error == null)
                return false;

            bool shouldBeProcessed = false;

            ErrorItem baseError = GetBaseError(error);

            if (baseError != null)
            {
                string uniqueKey = GetUniqueKey(baseError);
                long epochMinute = DateTime.UtcNow.ToUnixEpochMinutes();

                lock (errorToCounter)
                {
                    // get the counter for this error

                    if (errorToCounter.ContainsKey(uniqueKey))
                    {
                        // counter exists

                        Tuple<long, int> counter = errorToCounter[uniqueKey];

                        if (counter.Item1 == epochMinute)
                        {
                            // counter exists for this current minute
                            // increment the counter

                            Tuple<long, int> incCounter = new Tuple<long, int>(counter.Item1, counter.Item2 + 1);
                            errorToCounter[uniqueKey] = incCounter;

                            // check our throttling criteria

                            if (incCounter.Item2 <= MaxDupErrorPerMinute)
                            {
                                shouldBeProcessed = true;
                            }
                        }
                        else
                        {
                            // counter did not exist for this minute so overwrite the entry with a new one
                            errorToCounter[uniqueKey] = new Tuple<long, int>(epochMinute, 1);
                            shouldBeProcessed = true;
                        }
                    }
                    else
                    {
                        // counter did not exist so create a new one
                        errorToCounter[uniqueKey] = new Tuple<long, int>(epochMinute, 1);
                        shouldBeProcessed = true;
                    }

                    // see if we need to purge our counters of expired entries

                    if (nextErrorToCounterCleanUp < epochMinute)
                    {
                        nextErrorToCounterCleanUp = PurgeErrorToCounter(epochMinute);
                    }
                }
            }

            return shouldBeProcessed;
        }

        /// <summary>
        /// Gets the base error (the last error in the causal chain)
        /// </summary>
        /// <param name="error">The error</param>
        /// <returns>The inner most error</returns>
        private ErrorItem GetBaseError(StackifyError error)
        {
            ErrorItem errorItem = error.Error;

            if (errorItem != null)
            {
                while (errorItem.InnerError != null)
                {
                    errorItem = errorItem.InnerError;
                }
            }

            return errorItem;
        }

        /// <summary>
        /// Generates a unique key based on the error. The key will be an MD5 hask of the type, type code, and method.
        /// </summary>
        /// <param name="errorItem">The error item</param>
        /// <returns>The unique key for the error</returns>
        private string GetUniqueKey(ErrorItem errorItem)
        {
            string type = errorItem.ErrorType;
            string typeCode = errorItem.ErrorTypeCode;
            string method = errorItem.SourceMethod;

            string uniqueKey = string.Format("{0}-{1}-{2}", type ?? "", typeCode ?? "", method ?? "");

            return uniqueKey.ToMD5Hash();
        }

        /// <summary>
        /// Purges the errorToCounter dictionary of expired entries
        /// </summary>
        /// <param name="epochMinute">The current time</param>
        /// <returns>The next time the purge needs to run</returns>
        private long PurgeErrorToCounter(long epochMinute)
        {
            foreach (var entry in errorToCounter.Where(kvp => kvp.Value.Item1 < epochMinute).ToList())
            {
                errorToCounter.Remove(entry.Key);
            }

            return epochMinute + cleanUpMinutes;
        }
    }
}
