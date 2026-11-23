#nullable disable
namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// A node used by the access order (no expiry) weighted eviction policy. Stores the entry weight
    /// from the writer's perspective (<see cref="Weight"/>) and from the policy's perspective
    /// (<see cref="PolicyWeight"/>).
    /// </summary>
    internal sealed class WeightedAccessOrderNode<K, V> : LfuNode<K, V>
        where K : notnull
    {
        public WeightedAccessOrderNode(K k, V v)
            : base(k, v)
        {
        }

        // The weight from the writer's perspective. Set synchronously at write time, always up to date.
        public int Weight;

        // The weight from the policy's perspective. Set during maintenance, 0 until the write drains.
        public int PolicyWeight;
    }

    /// <summary>
    /// A node used by the time order (expiry) weighted eviction policy. Combines the time order links
    /// with the entry weight from the writer's perspective (<see cref="Weight"/>) and from the policy's
    /// perspective (<see cref="PolicyWeight"/>).
    /// </summary>
    internal sealed class WeightedTimeOrderNode<K, V> : TimeOrderNode<K, V>
        where K : notnull
    {
        public WeightedTimeOrderNode(K k, V v)
            : base(k, v)
        {
        }

        // The weight from the writer's perspective. Set synchronously at write time, always up to date.
        public int Weight;

        // The weight from the policy's perspective. Set during maintenance, 0 until the write drains.
        public int PolicyWeight;
    }
}
