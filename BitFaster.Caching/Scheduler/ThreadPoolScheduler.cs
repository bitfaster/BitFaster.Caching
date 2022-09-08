using System;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that handles queuing tasks to execute in the ThreadPool.
    /// </summary>
    public sealed class ThreadPoolScheduler : IScheduler
    {
        private long count;
        private Optional<Exception> lastException = Optional<Exception>.None();

        ///<inheritdoc/>
        public bool IsBackground => true;

        ///<inheritdoc/>
        public long RunCount => count;

        ///<inheritdoc/>
        public Optional<Exception> LastException => lastException;

        ///<inheritdoc/>
        public void Run(Action action)
        {
            count++;
            Task.Run(action)
                .ContinueWith(t => lastException = new Optional<Exception>(t.Exception.Flatten().InnerException), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
