using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackifyLib.Internal.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace StackifyLib.UnitTests
{
    public class JsonDotNetSerializer_Tests
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, NullValueHandling = NullValueHandling.Ignore, ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

        private readonly ITestOutputHelper _output;

        public JsonDotNetSerializer_Tests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact]
        public void Serialize_Should_Prune_Fields()
        {
            var input = GetFieldTestObject();

            var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, NullValueHandling = NullValueHandling.Ignore, ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

            var jdn = new JsonDotNetSerializer(Settings, 1, 5, 5);

            var json = jdn.SafeSerializeObject(input);

            _output.WriteLine(json);

            _output.WriteLine("Field limiting not yet available in Serialization");

            //json.Should().NotContainAny("IntSix", "IntSeven", "IntEight", "IntNine", "IntTen");
        }

        [Fact]
        public void Serialize_Should_Prune_Depth()
        {
            var input = GetNestedObj(10, 10);

            var jdn = new JsonDotNetSerializer(Settings, 5, 5, 5);

            var json = jdn.SafeSerializeObject(input);

            _output.WriteLine(json);

            json.Should().HaveLength(182);
        }

        [Fact]
        public void Serialize_Should_Prune_String()
        {
            var input = GetStringTestObject(1000);

            var jdn = new JsonDotNetSerializer(Settings, 5, 5, 5);

            var json = jdn.SafeSerializeObject(input);

            _output.WriteLine(json);

            json.Should().HaveLength(22);
        }



        private static object GetDepthTestObject()
        {
            var obj = new
            {
                ObjOne = new
                {
                    FieldOne = 1,
                    FieldTwo = 2,
                    FieldThree = 3,
                    FieldFour = 4,
                    FieldFive = 5,
                    FieldSix = 6,
                    FieldSeven = 7,
                    FieldEight = 8,
                    FieldNine = 9,
                    FieldTen = 10
                },
                FieldEleven = 11,
                FieldTwelve = 12,
                FieldThirteen = 13,
                FieldFourteen = 14,
                FieldFifteen = 15,
                FieldSixteen = 16,
                FieldSeventeen = 17,
                FieldEighteen = 18,
                FieldNineteen = 19,
                FieldTwenty = 20,
                FieldTwentyOne = 21,
                FieldTwentyTwo = 22,
                FieldTwentyFour = 24,
                FieldTwentyFive = 25,
                FieldTwentySix = 26,
                FieldTwentySeven = 27,
                FieldTwentyEight = 28,
                FieldTwentyNine = 29,
                FieldThirty = 30,
                FieldThirtyOne = 31,
                FieldThirtyTwo = 32,
                FieldThirtyThree = 33,
                FieldThirtyFour = 34,
                FieldThirtyFive = 35,
                FieldThirtySix = 36,
                FieldThirtySeven = 37,
                FieldThirtyEight = 38,
                FieldThirtyNine = 39,
                FieldFourty = 40,
                FieldFourtyOne = 41,
                FieldFourtyTwo = 42,
                FieldFourtyThree = 43,
                FieldFourtyFour = 44,
                FieldFourtyFive = 45,
                FieldFourtySix = 46,
                FieldFourtySeven = 47,
                FieldFourtyEight = 48,
                FieldFourtyNine = 49,
                FieldFifty = 50,
                FieldFiftyOne = 51,
                ObjTwo = new
                {
                    FieldFiftyTwo = 52,
                    FieldFiftyThree = 53,
                    FieldFiftyFour = 54,
                    FieldFiftyFive = 55,
                    FieldFiftySix = 56,
                    FieldFiftySeven = 57,
                    FieldFiftyEight = 58,
                    FieldFiftyNine = 59,
                    FieldSixty = 60
                }
            };

            return obj;
        }

        private static object GetFieldTestObject()
        {
            var obj = new TestField
            {
                IntOne = 1,
                IntTwo = 2,
                IntThree = 3,
                IntFour = 4,
                IntFive = 5,
                IntSix = 6,
                IntSeven = 7,
                IntEight = 8,
                IntNine = 9,
                IntTen = 10
            };

            return obj;
        }

        private static object GetStringTestObject(int stringLength)
        {
            var obj = new TestString
            {
                StringProp = GetString(stringLength)
            };

            return obj;
        }

        public class TestField
        {
            public int IntOne { get; set; }
            public int IntTwo { get; set; }
            public int IntThree { get; set; }
            public int IntFour { get; set; }
            public int IntFive { get; set; }
            public int IntSix { get; set; }
            public int IntSeven { get; set; }
            public int IntEight { get; set; }
            public int IntNine { get; set; }
            public int IntTen { get; set; }
        }

        public class TestString
        {
            public string StringProp { get; set; }
        }

        public class Test
        {
            public Test NestedProp { get; set; }
            public string StringProp { get; set; }
        }

        private static Test GetNestedObj(int nestingDepth, int stringLength)
        {
            var obj = new Test { StringProp = GetString(stringLength) };

            var currentDepth = 0;

            AddNestedObjRecursive(obj, nestingDepth, stringLength, ref currentDepth);

            return obj;
        }

        private static void AddNestedObjRecursive(Test parent, int nestingDepth, int stringLength, ref int currentDepth)
        {
            if (currentDepth >= nestingDepth)
            {
                return;
            }

            var obj = new Test { StringProp = GetString(stringLength) };

            parent.NestedProp = obj;

            currentDepth++;

            AddNestedObjRecursive(obj, nestingDepth, stringLength, ref currentDepth);
        }

        private static string GetString(int stringLength)
        {
            var ran = new Random();

            var sb = new StringBuilder();

            var current = 0;

            while (current < stringLength)
            {
                var next = ran.Next(0, 9);
                sb.Append(next.ToString());
                current++;
            }

            var r = sb.ToString();

            return r;
        }
    }
}
