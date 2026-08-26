using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.UnitTests.Lfu
{
    internal class WeightedTest
    {
        private const int DefaultMaxItems = 100000;
        private const int DefaultMaxItemBytes = 2 * 1024 * 1024;        
        private const long DefaultMaxTotalBytes = 250L * 1024 * 1024; 

        private const int MinTrimBatch = 16;
        private const int MaxTrimIterations = 64;

        private readonly int _maxItemBytes;
        private readonly long _maxTotalBytes;
        private long _currentBytes;

        private readonly ConcurrentDictionary<string, int> _sizes = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        private int _reconcileInProgress;

        public readonly ConcurrentLfu<string, string> _cache;

        internal WeightedTest(int maxItems, int maxItemBytes, long maxTotalBytes)
        {
            _maxItemBytes = maxItemBytes > 0 ? maxItemBytes : DefaultMaxItemBytes;
            _maxTotalBytes = maxTotalBytes > 0 ? maxTotalBytes : DefaultMaxTotalBytes;

            _cache = new ConcurrentLfu<string, string>(
                Environment.ProcessorCount,
                maxItems > 0 ? maxItems : DefaultMaxItems,
                new ForegroundScheduler(),
                EqualityComparer<string>.Default);
        }

        public void Populate(string key, string? value, int sizeBytes)
        {
            if (value is null || value.Length == 0 || sizeBytes > _maxItemBytes)
            {
                return;
            }

            if (_cache.TryGet(key, out _))
            {
                return;
            }

            _cache.AddOrUpdate(key, value);
            if (_sizes.TryAdd(key, value.Length))
            {
                Interlocked.Add(ref _currentBytes, value.Length);
            }

            this.EnforceByteBudget();
        }

        private void EnforceByteBudget()
        {
            if (Interlocked.CompareExchange(ref _reconcileInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                try
                {
                    if (_cache.Policy.Eviction.HasValue)
                    {
                        IBoundedPolicy eviction = _cache.Policy.Eviction.Value!;
                        int iterations = 0;
                        bool trimmed = false;

                        long estimatedBytes = Interlocked.Read(ref _currentBytes);
                        while (estimatedBytes > _maxTotalBytes && _cache.Count > 0 && iterations++ < MaxTrimIterations)
                        {
                            long over = estimatedBytes - _maxTotalBytes;
                            int entries = Math.Max(_sizes.Count, 1);
                            long avg = Math.Max(1, estimatedBytes / entries);
                            int toTrim = (int)Math.Min(_cache.Count, Math.Max(MinTrimBatch, (over / avg) + 1));

                            eviction.Trim(toTrim);
                            estimatedBytes -= toTrim * avg;
                            trimmed = true;
                        }

                        if (trimmed)
                        {
                            this.ReconcileSizes();
                        }
                    }

                    if (_sizes.Count > _cache.Count)
                    {
                        this.ReconcileSizes();
                    }
                }
#pragma warning disable CA1031 // Cache maintenance is best-effort: never fail the retrieve that just populated.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    throw;
                }
            }
            finally
            {
                Volatile.Write(ref _reconcileInProgress, 0);
            }
        }

        private void ReconcileSizes()
        {
            var live = new HashSet<string>(_cache.Keys, StringComparer.Ordinal);
            foreach (string key in _sizes.Keys)
            {
                if (!live.Contains(key) && _sizes.TryRemove(key, out int size))
                {
                    Interlocked.Add(ref _currentBytes, -(long)size);
                }
            }
        }
    }
}
