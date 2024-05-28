using System;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents an LRU item policy.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    /// <typeparam name="I">The type of the LRU item.</typeparam>
    public interface IItemPolicy<in K, in V, I> where I : LruItem<K, V>
        where K : notnull
    {
        /// <summary>
        /// Creates an LRU item.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">The item value.</param>
        /// <returns>An LRU item.</returns>
        I CreateItem(K key, V value);

        /// <summary>
        /// Touch an item on read.
        /// </summary>
        /// <param name="item">The item to touch.</param>
        void Touch(I item);

        /// <summary>
        /// Update an item.
        /// </summary>
        /// <param name="item">The item to update.</param>
        void Update(I item);

        /// <summary>
        /// Determine whether an item should be discarded.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>true if the item should be discarded, otherwise false.</returns>
        bool ShouldDiscard(I item);

        /// <summary>
        /// Gets a value indicating whether this policy can discard items.
        /// </summary>
        /// <returns>true if the policy can discard items, otherwise false.</returns>
        bool CanDiscard();

        /// <summary>
        /// Route a hot item.
        /// </summary>
        /// <param name="item">The item to route.</param>
        /// <returns>The destination for the specified item.</returns>
        ItemDestination RouteHot(I item);

        /// <summary>
        /// Route a warm item.
        /// </summary>
        /// <param name="item">The item to route.</param>
        /// <returns>The destination for the specified item.</returns>
        ItemDestination RouteWarm(I item);

        /// <summary>
        /// Route a cold item.
        /// </summary>
        /// <param name="item">The item to route.</param>
        /// <returns>The destination for the specified item.</returns>
        ItemDestination RouteCold(I item);

        /// <summary>
        /// The item time to live defined by the policy.
        /// </summary>
        TimeSpan TimeToLive { get; }
    }
}
