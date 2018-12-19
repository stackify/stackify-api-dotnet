using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StackifyLib.Internal.Serialization
{
    internal class SafeJsonTextReader : JsonTextReader
    {
        private readonly int _maxStringLength;

        public int CurrentDepth { get; private set; }
        public decimal CurrentFields { get; private set; } = 1;


        public SafeJsonTextReader(TextReader reader, int maxStringLength) : base(reader)
        {
            _maxStringLength = maxStringLength;
        }


        public override bool Read()
        {
            CurrentDepth = Path.Count(c => c == '.') + 1;
            CurrentFields += .5M;

            return base.Read();
        }

        public override string ReadAsString()
        {
            var value = base.ReadAsString();

            if (value != null)
            {
                if (value.Length > _maxStringLength)
                {
                    value = value.Substring(0, _maxStringLength);
                }
            }

            return value;
        }
    }
}
