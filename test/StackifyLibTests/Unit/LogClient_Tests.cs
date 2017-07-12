using System;
using System.Threading.Tasks;
using Moq;
using StackifyLib;
using StackifyLib.Auth;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using StackifyLib.Utils;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class LogClient_Tests
    {
        private readonly Mock<IScheduledLogHandler> _logHandlerMock;
        private readonly Mock<IErrorGovernor> _governorMock;
        public LogClient_Tests()
        {
            _logHandlerMock = new Mock<IScheduledLogHandler>();
            _governorMock = new Mock<IErrorGovernor>();
        }

        [Fact]
        public void CanQueue_Returns_True_If_Queue_Returns_True()
        {
            // arrange
            _logHandlerMock.Setup(q => q.CanQueue()).Returns(true);
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            var result = client.CanQueue();

            // assert
            Assert.True(result);
        }

        [Fact]
        public void CanQueue_Returns_False_If_Queue_Returns_False()
        {
            // arrange
            _logHandlerMock.Setup(q => q.CanQueue()).Returns(true);
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            var result = client.CanQueue();

            // assert
            Assert.True(result);
        }

        [Fact]
        public void Close_Calls_Stop_On_Queue()
        {
            // arrange
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            client.Close();

            // assert
            _logHandlerMock.Verify(q => q.Stop(), Times.Once);
        }

        [Fact]
        public void PauseUpload_Calls_Pause_On_Queue()
        {
            // arrange
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            client.PauseUpload(true);

            // assert
            _logHandlerMock.Verify(q => q.Pause(It.Is<bool>(b => b == true)), Times.Once);
        }

        [Fact]
        public void ErrorShouldBeSent_Returns_Response_From_Governor()
        {
            // arrange
            var error = new StackifyError(new Exception());

            _governorMock.Setup(g => g.ErrorShouldBeSent(It.IsAny<StackifyError>())).Returns(true);

            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            var result = client.ErrorShouldBeSent(error);

            // assert
            Assert.True(result);
        }

        [Fact]
        public void QueueMessage_Does_Not_Queue_Null_Message()
        {
            // arrange
            var claims = new AppClaims();
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            client.QueueMessage(null, claims);

            // assert
            _logHandlerMock.Verify(q => q.QueueLogMessage(It.IsAny<AppClaims>(), It.IsAny<LogMsg>()), Times.Never);
        }

        [Fact]
        public void QueueMessage_Does_Not_Queue_If_Queue_Is_Full()
        {
            // arrange
            _logHandlerMock.Setup(q => q.CanQueue()).Returns(false);
            var message = new LogMsg();
            var claims = new AppClaims();
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            client.QueueMessage(message, claims);

            // assert
            _logHandlerMock.Verify(q => q.QueueLogMessage(It.IsAny<AppClaims>(), It.IsAny<LogMsg>()), Times.Never);
        }

        [Fact]
        public void QueueMessage_Can_Queue_Message()
        {
            // arrange
            _logHandlerMock.Setup(q => q.CanQueue()).Returns(true);
            var message = new LogMsg();
            var claims = new AppClaims();
            var client = new LogClient(_logHandlerMock.Object, _governorMock.Object, string.Empty);

            // act
            client.QueueMessage(message, claims);

            // assert
            _logHandlerMock.Verify(q => q.QueueLogMessage(It.IsAny<AppClaims>(), It.IsAny<LogMsg>()), Times.Once);
        }
    }
}