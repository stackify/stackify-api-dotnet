using System;
using System.Threading;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Scheduling
{
    /// <summary>
    /// Manages a scheduled task. A new instance should be used for each task.
    /// </summary>
    internal class Scheduler : IScheduler
    {
        private Timer _timer;
        private TimerCallback _timerCallback;

        public bool IsStarted { get; private set; }

        public void Change(TimeSpan period)
        {
            _timer.Change(period, period);
        }

        public void Pause()
        {
            StackifyAPILogger.Log("Scheduler Paused");
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            IsStarted = false;
        }

        public void Schedule(TimerCallback callback, TimeSpan period)
        {
            StackifyAPILogger.Log("Received Schedule Request");
            if (_timer == null)
            {
                IsStarted = true;
                _timer = new Timer(callback, null, period, period);
                _timerCallback = callback;
            }
            else
            {
                if (callback == _timerCallback)
                {
                    IsStarted = true;
                    Change(period);
                }
                else
                {
                    StackifyAPILogger.Log("WARNING: Scheduler has already been assigned a callback. If you need to schedule a new task, use a different scheduler instance.");
                }
            }
        }
    }
}