namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A marker interface for discrete expiry policies.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public interface IDiscreteItemPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
        where K : notnull
    {
    }
}
