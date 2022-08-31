using System;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that runs tasks synchronously.
    /// </summary>
    public sealed class ForegroundScheduler : IScheduler
    {
        private long count;

        public bool IsBackground => false;

        public long RunCount => count;

        public Optional<Exception> LastException => Optional<Exception>.None();

        public void Run(Action action)
        {
            count++;
            action();
        }
    }
}
