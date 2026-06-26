namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents a capacity partition scheme for the internal window/main queues used by the LFU.
    /// </summary>
    internal interface ICapacityPartition
    {
        /// <summary>
        /// Gets the total capacity.
        /// </summary>
        int Capacity { get; }
    }
}
