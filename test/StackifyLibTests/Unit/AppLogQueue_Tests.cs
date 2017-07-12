using System;
using StackifyLib;
using StackifyLib.Auth;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Internal.Logs;
using StackifyLib.Models;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class AppLogQueue_Tests
    {
        [Fact]
        public void IsFull_Returns_False_If_Queue_Count_Is_Equal_To_Max() 
        {
            // arrange
            var queue = new AppLogQueues(0);

            // act
            var result = queue.IsFull;

            // assert
            Assert.True(result);
        }

        [Fact]
        public void IsFull_Returns_False_If_Queue_Count_Is_Less_Than_Max() 
        {
            // arrange
            var queue = new AppLogQueues(1);

            // act
            var result = queue.IsFull;

            // assert
            Assert.False(result);
        }

        [Fact]
        public void IsEmpty_Returns_True_If_Queue_Count_0() 
        {
            // arrange
            var queue = new AppLogQueues(1);

            // act
            var result = queue.IsEmpty;

            // assert
            Assert.True(result);
        }

        [Fact]
        public void IsEmpty_Returns_False_If_Queue_Count_Not_0() 
        {
            // arrange
            var queue = new AppLogQueues(1);
            queue.QueueMessage(new AppClaims(), new LogMsg());

            // act
            var result = queue.IsEmpty;

            // assert
            Assert.False(result);
        }

        [Fact]
        public void GetAppQueue_Returns_Correct_Queue_ForApp() 
        {
            // arrange
            var queue = new AppLogQueues(2);
            var expectedApp = new AppClaims { AppName = "app1" };
            var expectedLogId = Guid.NewGuid().ToString();
            queue.QueueMessage(expectedApp, new LogMsg { id = expectedLogId });
            queue.QueueMessage(new AppClaims(), new LogMsg());

            // act
            var result = queue.GetAppQueue(expectedApp);
            result.TryDequeue(out LogMsg log);
            
            // assert
            Assert.Equal(expectedLogId, log.id);
        }

        [Fact]
        public void RemoveOldMessagesFromQueue_Drops_Messages_Older_Than_5_Minutes() 
        {
            // arrange
            var queue = new AppLogQueues(1);
            var messageAge = (long)DateTime.UtcNow.AddMinutes(-5).Subtract(StackifyConstants.Epoch).TotalMilliseconds - 1;
            queue.QueueMessage(new AppClaims(), new LogMsg {
                EpochMs = messageAge
            });

            // act
            queue.RemoveOldMessagesFromQueue();
            var result = queue.IsEmpty;

            // assert
            Assert.True(result);
        }

        [Fact]
        public void RemoveOldMessagesFromQueue_DoesNotDrop_Messages_Newer_Than_5_Minutes() 
        {
            // arrange
            var queue = new AppLogQueues(1);
            var messageAge = (long)DateTime.UtcNow.AddMinutes(-4).Subtract(StackifyConstants.Epoch).TotalMilliseconds;
            queue.QueueMessage(new AppClaims(), new LogMsg {
                EpochMs = messageAge
            });

            // act
            queue.RemoveOldMessagesFromQueue();
            var result = queue.IsEmpty;

            // assert
            Assert.False(result);
        }

        [Fact]
        public void GetAppLogBatches_Correctly_Batches_Logs_For_Each_App() 
        {
            // arrange
            const int logsPerApp = 10;
             
            // 5 batches of 1 log each per app
            const int maxNumberOfBatches = 5;
            const int batchSize = 1;

            var queue = new AppLogQueues(100);
            var app1 = new AppClaims { AppName = "app1" };
            var app2 = new AppClaims { AppName = "app2" };
            for(var i = 0; i < logsPerApp; i++)
            {
                queue.QueueMessage(app1, new LogMsg());
                queue.QueueMessage(app2, new LogMsg());
            }

            // act
            var appLogBatches = queue.GetAppLogBatches(batchSize, maxNumberOfBatches);
            
            // assert
            Assert.Equal(2, appLogBatches.Count);
            Assert.Equal(maxNumberOfBatches, appLogBatches[app1].Count);
            Assert.Equal(batchSize, appLogBatches[app1][0].Count);
        }
    }
}