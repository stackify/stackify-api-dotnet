using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StackifyLib.Utils
{
    /// <summary>
    /// String extension methods
    /// </summary>
    internal static class StringExtensions
    {
        /// <summary>
        /// Calculates the MD5 hash of the string
        /// </summary>
        /// <returns>The MD5 hash of the string as a hex string</returns>
        public static string ToMD5Hash(this string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static string Right(this string sValue, int iMaxLength)
        {
            //Check if the value is valid
            if (string.IsNullOrEmpty(sValue))
            {
                //Set valid empty string as string could be null
                sValue = string.Empty;
            }
            else if (sValue.Length > iMaxLength)
            {
                //Make the string no longer than the max length
                sValue = sValue.Substring(sValue.Length - iMaxLength, iMaxLength);
            }

            //Return the string
            return sValue;
        }

        public static string Left(this string sValue, int iMaxLength)
        {
            //Check if the value is valid
            if (string.IsNullOrEmpty(sValue))
            {
                //Set valid empty string as string could be null
                sValue = string.Empty;
            }
            else if (sValue.Length > iMaxLength)
            {
                //Make the string no longer than the max length
                sValue = sValue.Substring(0, iMaxLength);
            }

            //Return the string
            return sValue;
        }

        public static string GetCleanName(this string name, bool isAppName = false)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            /*
            if (EnvironmentMemoryCache[name] is string cachedEnvironment)
            {
                // Found in cache, quick return of already processed value
                // cached value may be empty string if all characters are foreign characters
                return string.IsNullOrWhiteSpace(cachedEnvironment) ? null : cachedEnvironment;
            }

            if (isAppName)
            {
                if (ApplicationNameMemoryCache[name] is string cachedAppName)
                {
                    // Found in cache, quick return of already processed value
                    // cached value may be empty string if all characters are foreign characters
                    return string.IsNullOrWhiteSpace(cachedAppName) ? null : cachedAppName;
                }
            }
            */
            var strippedName = RemoveDiacritics(name);
            var final = new List<char>();
            var spaces = 0;
            for (int i = 0; i < strippedName.Length; i++)
            {
                var c = strippedName[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                {
                    final.Add(c);
                    spaces = 0;
                }
                else if ((char.IsPunctuation(c) || c == ' ') && spaces == 0 && c != '\'')
                {
                    final.Add(' ');
                    ++spaces;
                }
            }
            var cleanName = string.Join("", final).Trim();

            if (isAppName)
            {
                //ApplicationNameMemoryCache[name] = cleanName;
                if (string.IsNullOrWhiteSpace(cleanName))
                {
                    return string.Empty;
                }
            }
            else
            {
                //EnvironmentMemoryCache[name] = cleanName;
                if (string.IsNullOrWhiteSpace(cleanName))
                {
                    return string.Empty;
                }
            }

            return cleanName;
        }
        // https://stackoverflow.com/a/2086575/8121383
        private static string RemoveDiacritics(string name)
        {
            var tempBytes = Encoding.GetEncoding("ISO-8859-8").GetBytes(name);
            return Encoding.UTF8.GetString(tempBytes);
        }
    }
}
