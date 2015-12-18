using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.log4net.Tests
{
    [TestFixture]
    public class StackifyAppenderTest
    {
        private MockLogClient _mockLogClient;
        private StackifyAppender _appender;

        [OneTimeSetUp]
        public void Init()
        {
            _mockLogClient = new MockLogClient();
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            _appender = new StackifyAppender { CreateLogClient = (s, s1) => _mockLogClient, threadContextKeys = "TestThreadContext", logicalThreadContextKeys = "TestLogicalThreadContext" };
            _appender.ActivateOptions();
            hierarchy.Root.AddAppender(_appender);

            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
            Console.WriteLine("Setup Log4Net");
        }

        public void CleanUp()
        {
            // Just a bit of cleanup.
            ThreadContext.Properties["TestThreadContext"] = null;
            LogicalThreadContext.Properties["TestLogicalThreadContext"] = null;
            Console.WriteLine("Cleaned up Context");
        }

        [Test]
        public void ShouldIncludeContextObjects_WhenUsingLog4NetContextStacks()
        {
            LogMsg result = null;
            _mockLogClient.OnQueueMessage = m => result = m;
            var logger = LogManager.GetLogger("Test");

            using (LogicalThreadContext.Stacks["TestLogicalThreadContext"].Push("Logical Test Value"))
            using (ThreadContext.Stacks["TestThreadContext"].Push("Thread Test Value"))
            {
                logger.Info("Test Message");
            }

            Console.WriteLine("Actual Result:");
            Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
            Console.WriteLine();

            Assert.IsNotNull(result, "No Message was queued");
            Assert.IsNotNull(result.data, "Data was not set on result");

            // Data is JSON, so we are going to Deserialize it so we can take a deeper look. 
            dynamic dataObj = JsonConvert.DeserializeObject(result.data);

            Assert.AreEqual("Thread Test Value", dataObj.context.testthreadcontext.Value, "TestThreadContext didn't match expected value.");
            Assert.AreEqual("Logical Test Value", dataObj.context.testlogicalthreadcontext.Value, "TestLogicalThreadContext didn't match expected value");
        }

        [Test]
        public void ShouldIncludeContextObjects_WhenUsingLog4NetContextProperty()
        {
            LogMsg result = null;
            _mockLogClient.OnQueueMessage = m => result = m;
            var logger = LogManager.GetLogger("Test");
            LogicalThreadContext.Properties["TestLogicalThreadContext"] = new { MyProp = "Logical Test Value" };
            ThreadContext.Properties["TestThreadContext"] = new { MyProp = "Thread Test Value" };

            logger.Info("Test Message");

            Console.WriteLine("Actual Result:");
            Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
            Console.WriteLine();

            Assert.IsNotNull(result, "No Message was queued");
            Assert.IsNotNull(result.data, "Data was not set on result");

            // Data is JSON, so we are going to Deserialize it so we can take a deeper look. 
            dynamic dataObj = JsonConvert.DeserializeObject(result.data);

            Assert.AreEqual("Thread Test Value", dataObj.context.testthreadcontext.MyProp.Value, "TestThreadContext didn't match expected value.");
            Assert.AreEqual("Logical Test Value", dataObj.context.testlogicalthreadcontext.MyProp.Value, "TestLogicalThreadContext didn't match expected value");
        }
    }

    public class MockLogClient : ILogClient
    {
        public Action<LogMsg> OnQueueMessage;

        public bool CanQueue()
        {
            return true;
        }

        public bool CanSend()
        {
            return true;
        }

        public bool CanUpload()
        {
            return true;
        }

        public void Close()
        {
        }

        public bool ErrorShouldBeSent(StackifyError error)
        {
            return true;
        }

        public AppIdentityInfo GetIdentity()
        {
            throw new NotImplementedException();
        }

        public bool IsAuthorized()
        {
            return true;
        }

        public void PauseUpload(bool isPaused)
        {
        }

        public void QueueMessage(LogMsg msg)
        {
            var action = OnQueueMessage;
            if (action != null)
            {
                action.Invoke(msg);
            }
        }
    }
}
