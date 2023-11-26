using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    public interface INodePolicy<K, V, N>
        where N : LfuNode<K, V>
    {
        N Create(K key, V value);

        // TODO: expire methods
    }

    public struct AccessOrderPolicy<K, V> : INodePolicy<K, V, AccessOrderNode<K, V>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AccessOrderNode<K, V> Create(K key, V value)
        {
            return new AccessOrderNode<K, V>(key, value);
        }
    }

    public struct ExpireAfterPolicy<K, V> : INodePolicy<K, V, TimeOrderNode<K, V>>
    {
        private readonly TimerWheel<K, V> wheel;

        // TODO: expiry calculator

        public ExpireAfterPolicy(TimerWheel<K, V> wheel)
        {
            this.wheel = wheel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeOrderNode<K, V> Create(K key, V value)
        {
            return new TimeOrderNode<K, V>(key, value);
        }
    }
}
