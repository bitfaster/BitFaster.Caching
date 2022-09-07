
namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Specifies the destination for routing LRU items.
    /// </summary>
    public enum ItemDestination
    {
        /// <summary>
        /// Route to the warm queue.
        /// </summary>
        Warm,

        /// <summary>
        /// Route to the cold queue.
        /// </summary>
        Cold,

        /// <summary>
        /// Remove the item.
        /// </summary>
        Remove
    }	
}
