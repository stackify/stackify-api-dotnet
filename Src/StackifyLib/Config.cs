using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace StackifyLib
{
	/// <summary>
	/// Encapsulate settings retrieval mechanism. Currently supports config file and environment variables.
	/// Could be expanded to include other type of configuration servers later.
	/// </summary>
	sealed class Config
	{
		/// <summary>
		/// Attempts to fetch a setting value given the key.
		/// .NET configuration file will be used first, if the key is not found, environment variable will be used next.
		/// </summary>
		/// <param name="key">configuration key in config file or environment variable name.</param>
		/// <param name="defaultValue">If nothing is found, return optional defaultValue provided.</param>
		/// <returns>string value for the requested setting key.</returns>
		public static string Get(string key, string defaultValue = null)
		{
			string v = null;
			try
			{
				if (key != null)
				{
					v = ConfigurationManager.AppSettings[key];
					if (string.IsNullOrEmpty(v))
						v = Environment.GetEnvironmentVariable(key);
				}
			}
			finally
			{
				if (v == null)
					v = defaultValue;
			}
			return v;
		}
	}
}
