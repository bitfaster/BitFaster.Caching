using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

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
            List<K> keys = new List<K>();

            foreach (var kvp in kvps)
            {
                if (filter(kvp.Value))
                {
                    keys.Add(kvp.Key);
                }
            }

            return new ReadOnlyCollection<K>(keys);
        }
    }
}
