using System;
using System.Collections.Generic;
using System.Threading;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.UnitTests.Lfu
{
    // simple wrapper around ConcurrentLfu to trim the cache after a certain number of items have been added: repros reported bug
    internal class TrimmingLfuCache
    {
        private const int MinTrimBatch = 16;
        private const int MaxTrimIterations = 64;

        private readonly long trimAfter;

        private int trimInProgress;

        public readonly ConcurrentLfu<string, string> _cache;

        internal TrimmingLfuCache(int maxItems, long trimAfter)
        {
            this.trimAfter = trimAfter;

            _cache = new ConcurrentLfu<string, string>(
                Environment.ProcessorCount,
                maxItems ,
                new ForegroundScheduler(),
                EqualityComparer<string>.Default);
        }

        public void AddWithTrim(string key, string value)
        {
            if (_cache.TryGet(key, out _))
            {
                return;
            }

            _cache.AddOrUpdate(key, value);

            this.Trim();
        }

        private void Trim()
        {
            if (Interlocked.CompareExchange(ref trimInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (_cache.Policy.Eviction.HasValue)
                {
                    IBoundedPolicy eviction = _cache.Policy.Eviction.Value!;
                    int iterations = 0;

                    long currentCount = _cache.Count;
                    while (_cache.Count > trimAfter && currentCount > 0 && iterations++ < MaxTrimIterations)
                    {
                        long over = currentCount - trimAfter;
                        int toTrim = (int)Math.Min(currentCount, Math.Max(MinTrimBatch, (over) + 1));
                        eviction.Trim(toTrim);
                        currentCount = _cache.Count;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref trimInProgress, 0);
            }
        }
    }
}
