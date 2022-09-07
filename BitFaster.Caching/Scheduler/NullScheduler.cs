using System;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that does no scheduling. Scheduled Tasks will not be run.
    /// </summary>
    public sealed class NullScheduler : IScheduler
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
            // do nothing
        }
    }
}
