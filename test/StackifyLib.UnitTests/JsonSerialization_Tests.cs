using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StackifyLib.UnitTests
{
    public class JsonSerialization_Tests
    {
        private readonly ITestOutputHelper output;

        public JsonSerialization_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }


        [Fact]
        public void Should_Prune_Object()
        {
            var testMaxFields = GetTestObjectFields();

            var result = StackifyLib.Utils.HelperFunctions.SerializeDebugData(testMaxFields, false);

            output.WriteLine(result);

            var obj = JObject.Parse(result);

            Assert.True(obj.TryGetValue("invalid", out _));
            Assert.True(obj.TryGetValue("message", out _));
            Assert.Equal(2, obj.Count);
        }

        [Fact]
        public void Should_Prune_Object_Array()
        {
            var list = new List<object>();

            var testOne = GetTestObjectFields();
            list.Add(testOne);

            var testTwo = GetTestObjectFields();
            list.Add(testTwo);

            var result = StackifyLib.Utils.HelperFunctions.SerializeDebugData(list, false);

            output.WriteLine(result);

            var obj = JObject.Parse(result);

            Assert.True(obj.TryGetValue("invalid", out _));
            Assert.True(obj.TryGetValue("message", out _));
            Assert.Equal(2, obj.Count);
        }

        [Fact]
        public void Should_Throw_On_Max_Depth()
        {
            var obj = GetTestObjectDepth();
            var result = StackifyLib.Utils.HelperFunctions.SerializeDebugData(obj, false);
            Assert.Null(result);
        }


        private static object GetTestObjectDepth()
        {
            var obj = new
            {
                One = new
                {
                    Two = new
                    {
                        Three = new
                        {
                            Four = new
                            {
                                Five = new
                                {
                                    Six = new
                                    {
                                        Seven = new
                                        {
                                            Eight = new
                                            {
                                                Nine = new
                                                {
                                                    Ten = new
                                                    {

                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return obj;
        }

        private static object GetTestObjectFields()
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
    }
}
