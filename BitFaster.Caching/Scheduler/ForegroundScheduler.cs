using System;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that runs tasks synchronously.
    /// </summary>
    public sealed class ForegroundScheduler : IScheduler
    {
        private long count;

        ///<inheritdoc/>
        public bool IsBackground => false;

        ///<inheritdoc/>
        public long RunCount => count;

        ///<inheritdoc/>
        public Optional<Exception> LastException => Optional<Exception>.None();

        ///<inheritdoc/>
        public void Run(Action action)
        {
            count++;
            action();
        }
    }
}
