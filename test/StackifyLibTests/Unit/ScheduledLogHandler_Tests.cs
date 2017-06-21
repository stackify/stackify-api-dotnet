using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using StackifyLib;
using StackifyLib.Auth;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.Logs;
using StackifyLib.Internal.Scheduling;
using StackifyLib.Internal.StackifyApi;
using StackifyLib.Models;
using StackifyLib.Utils;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class ScheduledLogHandler_Tests
    {
        private readonly Mock<IStackifyApiService> _apiServiceMock;
        private readonly Mock<IScheduler> _schedulerMock;
        private readonly Mock<IAppLogQueues> _appLogQueuesMock;

        public ScheduledLogHandler_Tests()
        {
            _apiServiceMock = new Mock<IStackifyApiService>();
            _schedulerMock = new Mock<IScheduler>();
            _appLogQueuesMock = new Mock<IAppLogQueues>();
        }

        [Fact]
        public void CanQueue_Returns_False_If_Queue_Is_Full()
        {
            // arrange
            _appLogQueuesMock.Setup(q => q.IsFull).Returns(true);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            var result = queue.CanQueue();

            // assert
            Assert.False(result);
        }

        [Fact]
        public void CanQueue_Returns_True_If_Queue_Is_Not_Full()
        {
            // arrange
            _appLogQueuesMock.Setup(q => q.IsFull).Returns(false);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            var result = queue.CanQueue();

            // assert
            Assert.True(result);
        }

        [Fact]
        public void QueueLogMessage_Starts_Scheduler_If_Not_Started()
        {
            // arrange
            var claims = new AppClaims();
            var msg = new LogMsg();
            _schedulerMock.Setup(s => s.IsStarted).Returns(false);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            queue.QueueLogMessage(claims, msg);

            // assert
            _schedulerMock.Verify(s =>
                s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public void QueueLogMessage_Does_Not_Start_Scheduler_If_Started()
        {
            // arrange
            var claims = new AppClaims();
            var msg = new LogMsg();
            _schedulerMock.Setup(s => s.IsStarted).Returns(true);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            queue.QueueLogMessage(claims, msg);

            // assert
            _schedulerMock.Verify(s =>
                s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public void QueueLogMessage_Adds_Message_To_Queue()
        {
            // arrange
            var claims = new AppClaims();
            var msg = new LogMsg();
            _schedulerMock.Setup(s => s.IsStarted).Returns(true);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            queue.QueueLogMessage(claims, msg);

            // assert
            _appLogQueuesMock
                .Verify(q => q.QueueMessage(It.Is<AppClaims>(c => c == claims), It.IsAny<LogMsg>()), Times.Once);
        }

        [Fact]
        public void TimerCallback_Pauses_Scheduler()
        {
            // arrange
            var claims = new AppClaims();
            var msg = new LogMsg();

            TimerCallback callback = null;
            _schedulerMock
                .Setup(s => s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, TimeSpan>((c, t) => callback = c);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            queue.QueueLogMessage(claims, msg);
            callback.Invoke(null);

            // assert
            _schedulerMock.Verify(s => s.Pause(), Times.Once);
        }

        [Fact]
        public void TimerCallback_Removes_Old_Messages()
        {
            // arrange
            var claims = new AppClaims();
            var msg = new LogMsg();

            TimerCallback callback = null;
            _schedulerMock
                .Setup(s => s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, TimeSpan>((c, t) => callback = c);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            queue.QueueLogMessage(claims, msg);
            callback.Invoke(null);

            // assert
            _appLogQueuesMock.Verify(q => q.RemoveOldMessagesFromQueue(), Times.Once);
        }

        [Fact]
        public void TimerCallback_Sends_App_Logs()
        {
            // arrange
            var batches = GetAppLogBatches(1, 1, 1);

            _appLogQueuesMock
                .Setup(q => q.GetAppLogBatches(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(batches);

            TimerCallback callback = null;
            _schedulerMock
                .Setup(s => s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, TimeSpan>((c, t) => callback = c);
            var queue = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            queue.QueueLogMessage(new AppClaims(), new LogMsg());
            callback.Invoke(null);

            // assert
            _apiServiceMock.Verify(q => 
                q.UploadAsync(
                    It.IsAny<AppClaims>(),
                    It.IsAny<string>(),
                    It.IsAny<LogMsgGroup>(),
                    It.Is<bool>(b => b == true)), 
                Times.Once);
        }

        [Fact]
        public void TimerCallback_Sends_App_Logs_PerApp_PerBatch()
        {
            // arrange
            const int apps = 4;
            const int numberOfBatches = 4;
            var batches = GetAppLogBatches(apps, numberOfBatches, 1);

            _appLogQueuesMock
                .Setup(q => q.GetAppLogBatches(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(batches);

            TimerCallback callback = null;
            _schedulerMock
                .Setup(s => s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, TimeSpan>((c, t) => callback = c);
            var scheduledLogHandler = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            // act
            scheduledLogHandler.QueueLogMessage(new AppClaims(), new LogMsg());
            callback.Invoke(null);

            // assert
            _apiServiceMock.Verify(q => 
                q.UploadAsync(
                    It.IsAny<AppClaims>(),
                    It.IsAny<string>(),
                    It.IsAny<LogMsgGroup>(),
                    It.Is<bool>(b => b == true)), 
                Times.Exactly(apps * numberOfBatches));
        }

        [Fact]
        public void Http202_Response_DoesNot_ReQueue_Message()
        {
            TestReQueue(HttpStatusCode.Accepted, false);
        }

        [Fact]
        public void Http0_Response_DoesNot_ReQueue_Message()
        {
            TestReQueue((HttpStatusCode)0, false);
        }

        [Fact]
        public void Http400_Response_DoesNot_ReQueues_Message()
        {
            TestReQueue(HttpStatusCode.BadRequest, false);
        }

        [Fact]
        public void Http429_Response_DoesNot_ReQueues_Message()
        {
            TestReQueue((HttpStatusCode)429, false);
        }

        [Fact]
        public void Http500_Response_ReQueues_Message()
        {
            TestReQueue(HttpStatusCode.InternalServerError, true);
        }

        private void TestReQueue(HttpStatusCode statusCodeToReturn, bool shouldRequeue)
        {
            // arrange
            const int apps = 1;
            const int numberOfBatches = 1;
            var expectedRequeueCalls = shouldRequeue ? apps * numberOfBatches : 0;

            var batches = GetAppLogBatches(apps, numberOfBatches, 1);

            _appLogQueuesMock
                .Setup(q => q.GetAppLogBatches(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(batches);

            TimerCallback callback = null;
            _schedulerMock
                .Setup(s => s.Schedule(It.IsAny<TimerCallback>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, TimeSpan>((c, t) => callback = c);
            var scheduledLogHandler = new ScheduledLogHandler(_apiServiceMock.Object, _schedulerMock.Object, _appLogQueuesMock.Object);

            _apiServiceMock
                .Setup(m => m.UploadAsync(It.IsAny<AppClaims>(), It.IsAny<string>(), It.IsAny<Identifiable>(), It.IsAny<bool>()))
                .ReturnsAsync(statusCodeToReturn);

            // act
            scheduledLogHandler.QueueLogMessage(new AppClaims(), new LogMsg());
            callback.Invoke(null);

            // assert
            _appLogQueuesMock.Verify(q => 
                q.ReQueueBatch(It.IsAny<AppClaims>(), It.IsAny<List<LogMsg>>()),
                Times.Exactly(expectedRequeueCalls));
        }

        private Dictionary<AppClaims, List<List<LogMsg>>> GetAppLogBatches(int numberOfApps, int numberOfBatches, int batchSize)
        {
            var appLogBatches = new Dictionary<AppClaims, List<List<LogMsg>>>();
            for(var app = 0; app < numberOfApps; app++)
            {
                var appLogs = new List<List<LogMsg>>();
                var appClaims = new AppClaims {
                    AppName = $"app{app}"
                };
                appLogBatches[appClaims] = appLogs;

                for(var batch = 0; batch < numberOfBatches; batch++)
                {
                    var logBatch = new List<LogMsg>();
                    for(var log = 0; log < batchSize; log++)
                    {
                        logBatch.Add(new LogMsg());
                    }
                    appLogs.Add(logBatch);
                }
            }
            return appLogBatches;
        }
    }
}