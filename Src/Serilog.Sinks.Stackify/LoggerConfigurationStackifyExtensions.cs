// Copyright 2015 John Knoll <jpknoll@gmail.com>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.Stackify;

namespace Serilog
{
    /// <summary>
    /// Adds the WriteTo.Stackify() extension method to <see cref="LoggerConfiguration"/>.
    /// </summary>
    public static class LoggerConfigurationStackifyExtensions
    {
        /// <summary>
        /// Adds a sink that writes log events to the elmah.io webservice. 
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink. Set to Verbose by default.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration Stackify(
            this LoggerSinkConfiguration loggerConfiguration,
            IFormatProvider formatProvider = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum
            )
        {
            if (loggerConfiguration == null) throw new ArgumentNullException("loggerConfiguration");

            return loggerConfiguration
                .Sink(new StackifySink(formatProvider), restrictedToMinimumLevel);
        }
    }
}
