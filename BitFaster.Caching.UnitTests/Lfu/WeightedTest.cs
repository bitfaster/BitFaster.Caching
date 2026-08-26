using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using Castle.Core.Logging;

namespace BitFaster.Caching.UnitTests.Lfu
{
    internal class WeightedTest
    {
        private const int DefaultMaxItems = 100000;
        private const int DefaultMaxItemBytes = 2 * 1024 * 1024;        // 2 MB per-item cap (telemetry: a 1 MB cap drops
                                                                        // ~446K HOT fetches/day of 1-2 MB resources; 2 MB
                                                                        // excludes only ~0.28%; the rare multi-MB tail is
                                                                        // kept out by frequency eviction + the byte budget).
        private const long DefaultMaxTotalBytes = 250L * 1024 * 1024;  // 250 MB total resident budget

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

            // ForegroundScheduler runs maintenance (including the eviction the policy performs) inline on the calling
            // thread instead of the thread pool, so the trim + size reconciliation below is deterministic.
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

            // A given key always maps to identical content (content hash, or org+id+version), so if it is already
            // cached there is nothing to store — the TryGet here also bumps its frequency.
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
            // Single-writer gate: under the parallel retrieve fan-out many threads can Populate over-budget at once.
            // Without this gate each would independently run the full trim loop (ProcessorCount x the work); here only
            // the winner trims and reconciles, others return immediately without blocking. Any residual over-budget is
            // picked up by the next Populate, so eventual convergence holds.
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

                        // Drive the trim loop off an ESTIMATED byte decrement (toTrim * avg) and reconcile the size map
                        // exactly ONCE after the loop settles — instead of the authoritative O(live-keys) reconcile after
                        // every Trim. That keeps the reconciliation cost off the inner loop (previously up to
                        // MaxTrimIterations reconciles per Populate); the final ReconcileSizes restores the exact total,
                        // and if the estimate left us marginally over budget the next Populate trims the remainder.
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

                        // Reconcile only when we actually trimmed (i.e. were over budget). Reconciling on every Populate
                        // would race with concurrent adds — a just-added key may not yet be visible in _cache.Keys, so its
                        // bytes would be wrongly subtracted — undercounting resident bytes under budget.
                        if (trimmed)
                        {
                            this.ReconcileSizes();
                        }
                    }

                    // Catch silent capacity evictions that may have left stale size entries even while under budget.
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
                    //Logger.LogWarning(ex, "WebResourceContentCache.EnforceByteBudget threw; swallowing. Cache may temporarily exceed the byte budget.");
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
