using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StackifyLib.Internal.Serialization
{
    internal class ShouldSerializeContractResolver : DefaultContractResolver
    {
        private readonly Func<bool> _include;

        public ShouldSerializeContractResolver(Func<bool> include)
        {
            _include = include;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            property.ShouldSerialize = obj => _include();

            return property;
        }
    }
}
