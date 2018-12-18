using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace StackifyLib.Internal
{
    /// <summary>
    /// Source: https://stackoverflow.com/a/29684280/10100417
    /// </summary>
    public class MaxDepthJsonTextWriter : JsonTextWriter
    {
        public int? MaxDepth { get; set; }
        public int MaxObservedDepth { get; private set; }

        public MaxDepthJsonTextWriter(TextWriter writer, JsonSerializerSettings settings) : base(writer)
        {
            this.MaxDepth = (settings == null ? null : settings.MaxDepth);
            this.MaxObservedDepth = 0;
        }

        public MaxDepthJsonTextWriter(TextWriter writer, int? maxDepth) : base(writer)
        {
            this.MaxDepth = maxDepth;
        }

        public override void WriteStartArray()
        {
            base.WriteStartArray();
            CheckDepth();
        }

        public override void WriteStartConstructor(string name)
        {
            base.WriteStartConstructor(name);
            CheckDepth();
        }

        public override void WriteStartObject()
        {
            base.WriteStartObject();
            CheckDepth();
        }

        private void CheckDepth()
        {
            MaxObservedDepth = Math.Max(MaxObservedDepth, Top);

            if (Top > MaxDepth)
            {
                throw new JsonSerializationException(string.Format("Depth {0} Exceeds MaxDepth {1} at path \"{2}\"", Top, MaxDepth, Path));
            }
        }
    }
}
