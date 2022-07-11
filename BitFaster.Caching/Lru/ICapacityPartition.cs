using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents a partitioning scheme for the internal queues within concurrent LRU.
    /// </summary>
    /// <remarks>
    /// In general, increasing the size of the hot queue favors recent items and increasing
    /// the size of the warm queue favors frequent items.
    /// </remarks>
    public interface ICapacityPartition
    {
        /// <summary>
        /// Gets the capacity of the cold queue.
        /// </summary>
        int Cold { get; }

        /// <summary>
        /// Gets the capacity of the warm queue.
        /// </summary>
        int Warm { get; }

        /// <summary>
        /// Gets the capacity of the hot queue.
        /// </summary>
        int Hot { get; }
    }
}
