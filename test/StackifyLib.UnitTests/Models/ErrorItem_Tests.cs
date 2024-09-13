// Copyright (c) 2024 BMC Software, Inc.
// Copyright (c) 2021-2024 Netreo
// Copyright (c) 2019 Stackify
using StackifyLib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace StackifyLib.UnitTests.Models
{
    public class ErrorItem_Tests
    {

        [Fact]
        public async Task TestExceptionStringWithInnerException()
        {
            var expectedString = string.Join(
                    Environment.NewLine,
                    "System.Exception: Wrapping the exception to see the differences between outer and inner exceptions. ---> System.Exception: Throwing this exception to see all properties like stacktrace filled",
                    "  at StackifyLib.UnitTests.Models.ExampleService.ThrowInnerException()",
                    "  at StackifyLib.UnitTests.Models.ExampleService.CreateCaughtException()",
                    "--- End of inner exception stack trace ---",
                    "  at StackifyLib.UnitTests.Models.ExampleService.ThrowOuterException(System.Exception)",
                    "  at StackifyLib.UnitTests.Models.ExampleService.CreateCaughtException()",
                    "  at StackifyLib.UnitTests.Models.ErrorItem_Tests+<TestExceptionStringWithInnerException>d__0.MoveNext()",
                    ""
                );
            try
            {
                //ExampleService service = new ExampleService();
                ExampleService.CreateCaughtException();
            }
            catch (Exception ex)
            {
                //PrintExceptionInfo(ex);
                var exceptionString = new ErrorItem(ex).ToString();
                Assert.Contains(expectedString, exceptionString);
            }
        }
    }

    class ExampleService
    {
        public static Exception CreateCaughtException()
        {
            try
            {
                try
                {
                    ThrowInnerException();
                }
                catch (Exception exception)
                {
                    ThrowOuterException(exception);
                }
            }
            catch (Exception)
            {
                throw;
            }
            // NOTE: never reached, but needed for the compiler.
            return null;
        }
        private static void ThrowOuterException(Exception exception)
        {
            throw new Exception("Wrapping the exception to see the differences between outer and inner exceptions.", exception);
        }
        private static void ThrowInnerException()
        {
            throw new Exception("Throwing this exception to see all properties like stacktrace filled");
        }
    }
}
