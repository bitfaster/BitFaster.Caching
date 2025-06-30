using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BitFaster.Caching.Atomic
{
    internal static class AtomicEx
    {
        internal static int EnumerateCount(IEnumerator enumerator)
        {
            int count = 0;
            while (enumerator.MoveNext())
            {
                count++;
            }
            return count;
        }

        internal static ICollection<K> FilterKeys<K, V>(IEnumerable<KeyValuePair<K, V>> kvps, Func<V, bool> filter)
        {
#pragma warning disable CA1851
            // Here we will double enumerate the kvps list. Alternative is to lazy init the size which will keep resizing
            // the List, and spam allocs if the list is long.
            List<K> keys = new List<K>(kvps.Count());

            foreach (var kvp in kvps)
            {
                if (filter(kvp.Value))
                {
                    keys.Add(kvp.Key);
                }
            }

            return new ReadOnlyCollection<K>(keys);
#pragma warning restore CA1851
        }
    }
}
