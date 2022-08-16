using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that handles the low-level work of queuing tasks onto threads.
    /// </summary>
    public interface IScheduler
    {
        bool IsBackground { get; }

        /// <summary>
        /// Gets the count of scheduled work items.
        /// </summary>
        long RunCount { get; }

        /// <summary>
        /// Queues the specified work to run.
        /// </summary>
        /// <param name="action">The work to execute.</param>
        void Run(Action action);

        /// <summary>
        /// Gets the last exception, if any.
        /// </summary>
        Optional<Exception> LastException { get; }
    }
}
