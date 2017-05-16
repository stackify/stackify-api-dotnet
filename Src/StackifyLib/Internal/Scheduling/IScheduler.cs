using System;
using System.Threading;

namespace StackifyLib.Internal.Scheduling
{
    internal interface IScheduler
    {
        /// <summary>
        /// Whether or not the underlying timer has been created
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// Schedule a recurring task
        /// </summary>
        void Schedule(TimerCallback callback, TimeSpan period);

        /// <summary>
        /// Stop the underlyinf timer
        /// </summary>
        void Pause();

        /// <summary>
        /// Adjust the current callback interval
        /// </summary>
        void Change(TimeSpan period);
    }
}