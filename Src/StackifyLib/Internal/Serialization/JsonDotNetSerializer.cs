using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace StackifyLib.Internal.Serialization
{
    public class JsonDotNetSerializer
    {
        private readonly JsonSerializerSettings _settings;
        private readonly int _maxDepth;
        private readonly int _maxFields;
        private readonly int _maxStringLength;


        public JsonDotNetSerializer(JsonSerializerSettings settings = null, int? maxDepth = null, int? maxFields = null, int? maxStringLength = null)
        {
            _settings = settings;

            _maxDepth = maxDepth ?? Config.LoggingMaxDepth;
            _maxFields = maxFields ?? Config.LoggingMaxFields;
            _maxStringLength = maxStringLength ?? Config.LoggingMaxStringLength;
        }


        /// <summary>
        /// Serialize an object to JSON with limitations on MaxDepth and MaxStringLength as provided in the ctor.
        ///
        /// Note, Serialization does not currently consider MaxFields.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public string SafeSerializeObject(object obj)
        {
            string r;

            using (var writer = new StringWriter())
            {
                using (var jsonWriter = new SafeJsonTextWriter(writer, _maxStringLength))
                {
                    // ReSharper disable AccessToDisposedClosure
                    bool IncludeDepth() => jsonWriter.CurrentDepth <= _maxDepth;
                    // ReSharper restore AccessToDisposedClosure

                    var resolver = new ShouldSerializeContractResolver(IncludeDepth);
                    var serializer = JsonSerializer.CreateDefault(_settings);
                    serializer.ContractResolver = resolver;

                    serializer.Serialize(jsonWriter, obj);
                    r = writer.ToString();
                }
            }

            return r;
        }
    }
}
