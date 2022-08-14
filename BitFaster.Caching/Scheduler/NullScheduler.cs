using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that does no scheduling. Scheduled Tasks will not be run.
    /// </summary>
    public class NullScheduler : IScheduler
    {
        private long count;

        public long RunCount => count;

        public Task Next => Task.CompletedTask;

        public Optional<Exception> LastException => Optional<Exception>.None();

        public void Run(Action action)
        {
            count++;
            // do nothing
        }
    }
}
