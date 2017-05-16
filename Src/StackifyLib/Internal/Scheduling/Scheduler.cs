using System;
using System.Threading;

namespace StackifyLib.Internal.Scheduling
{
    internal class Scheduler : IScheduler
    {
        private Timer _timer;

        public bool IsStarted { get; private set; }

        public void Change(TimeSpan period)
        {
            _timer.Change(period, period);
        }

        public void Pause()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Schedule(TimerCallback callback, TimeSpan period)
        {
            if (_timer == null)
            {
                IsStarted = true;
                _timer = new Timer(callback, null, period, period);
            }
        }
    }
}