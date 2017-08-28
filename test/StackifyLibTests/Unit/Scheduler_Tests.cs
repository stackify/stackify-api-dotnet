using System;
using System.Threading;
using StackifyLib.Internal.Scheduling;
using Xunit;

namespace StackifyLibTests.Unit
{
    public class Scheduler_Tests
    {
        [Fact]
        public void Can_Schedule_Task()
        {
            // arrange
            const int expectedExecutionCount = 2;
            const int intervalMilliseconds = 500;
            const int sleepInterval = intervalMilliseconds * (expectedExecutionCount + 1) - 1;

            var executionCount = 0;
            void Callback(object state) => executionCount += 1;
            var scheduler = new Scheduler();

            // act
            scheduler.Schedule(Callback, TimeSpan.FromMilliseconds(intervalMilliseconds));
            Thread.Sleep(sleepInterval);

            // assert
            Assert.Equal(expectedExecutionCount, executionCount);
        }

        [Fact]
        public void Can_Pause_Task()
        {
            // arrange
            const int expectedExecutionCount = 1;
            const int intervalMilliseconds = 500;
            const int sleepInterval = intervalMilliseconds * (expectedExecutionCount + 1) - 1;

            var executionCount = 0;
            void Callback(object state) => executionCount += 1;
            var scheduler = new Scheduler();

            // act
            scheduler.Schedule(Callback, TimeSpan.FromMilliseconds(intervalMilliseconds));

            Thread.Sleep(sleepInterval);

            scheduler.Pause();

            Thread.Sleep(sleepInterval);

            // assert
            Assert.Equal(expectedExecutionCount, executionCount);
        }

        [Fact]
        public void Can_ReSchedule_Using_Same_Handler()
        {
            // arrange
            const int expectedExecutionCount = 1;
            const int intervalMilliseconds = 500;
            const int sleepInterval = intervalMilliseconds * (expectedExecutionCount + 1) - 1;

            var executionCount = 0;
            void Callback(object state) => executionCount += 1;

            var scheduler = new Scheduler();

            // act
            scheduler.Schedule(Callback, TimeSpan.FromMilliseconds(intervalMilliseconds));

            Thread.Sleep(sleepInterval);

            scheduler.Pause();

            scheduler.Schedule(Callback, TimeSpan.FromMilliseconds(intervalMilliseconds));

            Thread.Sleep(sleepInterval);

            // assert
            Assert.Equal(expectedExecutionCount * 2, executionCount);
        }

        [Fact]
        public void Cannot_ReSchedule_Using_Different_Handler()
        {
            // arrange
            const int expectedExecutionCount = 1;
            const int intervalMilliseconds = 500;
            const int sleepInterval = intervalMilliseconds * (expectedExecutionCount + 1) - 1;

            var executionCount = 0;
            void Callback1(object state) => executionCount += 1;
            void Callback2(object state) => Callback1(state);

            var scheduler = new Scheduler();

            // act
            scheduler.Schedule(Callback1, TimeSpan.FromMilliseconds(intervalMilliseconds));

            Thread.Sleep(sleepInterval);

            scheduler.Pause();

            scheduler.Schedule(Callback2, TimeSpan.FromMilliseconds(intervalMilliseconds));

            Thread.Sleep(sleepInterval);

            // assert
            Assert.Equal(expectedExecutionCount, executionCount);
        }
    }
}
