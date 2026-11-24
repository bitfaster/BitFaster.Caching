namespace BitFaster.Caching.Lfu
{
    internal sealed class WeightedAccessOrderNode<K, V> : LfuNode<K, V>
        where K : notnull
    {
        internal WeightedAccessOrderNode(K key, V value, int weight)
            : base(key, value)
        {
            this.Weight = weight;
        }

        internal volatile int Weight;

        // Only maintenance changes PolicyWeight: buffered updates must discount the last accounted weight.
        internal int PolicyWeight;
    }

    internal sealed class WeightedTimeOrderNode<K, V> : TimeOrderNode<K, V>
        where K : notnull
    {
        internal WeightedTimeOrderNode(K key, V value, int weight)
            : base(key, value)
        {
            this.Weight = weight;
        }

        internal volatile int Weight;
        internal int PolicyWeight;
    }
}
