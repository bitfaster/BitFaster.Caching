using System;
using System.Collections.Generic;

#if DEBUG
using System.Linq;
using System.Text;
#endif

namespace BitFaster.Caching.Lfu
{
    // The core is copied by alternate lookups and IBoundedPolicy. Keep all eviction state reference-owned.
    internal abstract class LfuEvictionPolicy<K, V>
        where K : notnull
    {
        protected readonly LfuNodeList<K, V> windowLru = new();
        protected readonly LfuNodeList<K, V> probationLru = new();
        protected readonly LfuNodeList<K, V> protectedLru = new();

        protected LfuEvictionPolicy(LfuCapacityPartition capacity)
        {
            this.Capacity = capacity;
        }

        internal LfuCapacityPartition Capacity { get; }

        internal LfuNodeList<K, V> Window => this.windowLru;
        internal LfuNodeList<K, V> Probation => this.probationLru;
        internal LfuNodeList<K, V> Protected => this.protectedLru;

        protected int Count => this.windowLru.Count + this.probationLru.Count + this.protectedLru.Count;

        internal List<LfuNode<K, V>> GetTrimCandidates(int itemCount)
        {
            itemCount = Math.Min(itemCount, this.Count);
            var candidates = new List<LfuNode<K, V>>(itemCount);

            TakeCandidatesInLruOrder(this.probationLru, candidates, itemCount);
            TakeCandidatesInLruOrder(this.protectedLru, candidates, itemCount);
            TakeCandidatesInLruOrder(this.windowLru, candidates, itemCount);

            return candidates;
        }

        private static void TakeCandidatesInLruOrder(LfuNodeList<K, V> lru, List<LfuNode<K, V>> candidates, int itemCount)
        {
            var current = lru.First;

            while (candidates.Count < itemCount && current != null)
            {
                if (!current.WasRemoved)
                {
                    candidates.Add(current);
                }

                current = current.Next;
            }
        }

#if DEBUG
        internal string FormatLfuString()
        {
            const int maxDebugLruDisplay = 99;
            var sb = new StringBuilder();

            sb.Append("W [");
            sb.Append(string.Join(",", this.windowLru.Select(n => n.Key.ToString()).Take(maxDebugLruDisplay)));
            sb.Append("] Protected [");
            sb.Append(string.Join(",", this.protectedLru.Select(n => n.Key.ToString()).Take(maxDebugLruDisplay)));
            sb.Append("] Probation [");
            sb.Append(string.Join(",", this.probationLru.Select(n => n.Key.ToString()).Take(maxDebugLruDisplay)));
            sb.Append(']');

            return sb.ToString();
        }
#endif
    }
}
