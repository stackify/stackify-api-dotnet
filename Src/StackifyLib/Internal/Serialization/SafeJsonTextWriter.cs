using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace StackifyLib.Internal.Serialization
{
    internal class SafeJsonTextWriter : JsonTextWriter
    {
        private readonly int _maxStringLength;

        public int CurrentDepth { get; private set; }


        public SafeJsonTextWriter(TextWriter writer, int maxStringLength) : base(writer)
        {
            _maxStringLength = maxStringLength;
        }


        public override void WriteStartObject()
        {
            CurrentDepth++;

            base.WriteStartObject();
        }

        public override void WriteEndObject()
        {
            CurrentDepth--;

            base.WriteEndObject();
        }

        public override void WriteValue(string value)
        {
            if (value.Length > _maxStringLength)
            {
                value = value.Substring(0, _maxStringLength);
            }

            base.WriteValue(value);
        }
    }
}
