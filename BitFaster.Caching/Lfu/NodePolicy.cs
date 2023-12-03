using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    internal interface INodePolicy<K, V, N>
        where N : LfuNode<K, V>
    {
        N Create(K key, V value);
    }

    internal struct AccessOrderPolicy<K, V> : INodePolicy<K, V, AccessOrderNode<K, V>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AccessOrderNode<K, V> Create(K key, V value)
        {
            return new AccessOrderNode<K, V>(key, value);
        }
    }
}
