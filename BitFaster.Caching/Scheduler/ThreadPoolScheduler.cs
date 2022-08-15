using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that handles queuing tasks to execute in the ThreadPool.
    /// </summary>
    public class ThreadPoolScheduler : IScheduler
    {
        private long count;
        private Optional<Exception> lastException = Optional<Exception>.None();

        public long RunCount => count;

        public Optional<Exception> LastException => lastException;

        public void Run(Action action)
        {
            count++;

            Task.Run(action);
        }
    }

    public class ThreadPoolFactoryScheduler : IScheduler
    {
        private long count;
        private Optional<Exception> lastException = Optional<Exception>.None();

        public long RunCount => count;

        public Optional<Exception> LastException => lastException;

        public void Run(Action action)
        {
            count++;

            Task.Factory.StartNew(action);

            // Same perf as Task.Run
            //ThreadPool.QueueUserWorkItem(delegate { action(); });
        }
    }
}
