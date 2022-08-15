using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that handles queuing tasks to execute in the ThreadPool.
    /// </summary>
    public class ThreadPoolSchedulerContinuations : IScheduler
    {
        private long count;
        private Optional<Exception> lastException = Optional<Exception>.None();

        public long RunCount => count;

        public Optional<Exception> LastException => lastException;

        public void Run(Action action)
        {
            count++;
            var task = Task.Run(action);
            task.ContinueWith(t => lastException = new Optional<Exception>(t.Exception.Flatten().InnerException), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
