using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class ConfigFactory
    {
        const double s = 0.86;          // Zipf s parameter, controls distribution
        const int n = 500;              // number of unique items for Zipf
        const int maxThreads = 52;
        const int sampleCount = 2000;

        public static (ThroughputBenchmarkBase, IThroughputBenchConfig, int) Create(Mode mode, int repeatCount)
        {
            switch (mode)
            {
                case Mode.Read:
                    return (new ReadThroughputBenchmark(), new ZipfConfig(repeatCount, sampleCount, s, n), n);
                case Mode.ReadWrite:
                    // cache holds 10% of all items
                    return (new ReadThroughputBenchmark(), new ZipfConfig(repeatCount, sampleCount, s, n), n / 10);
                case Mode.Update:
                    return (new UpdateThroughputBenchmark(), new ZipfConfig(repeatCount, sampleCount, s, n), n);
                case Mode.Evict:

                    int cacheSize = 10_000;
                    int evictSamples = Math.Min(10_000_000, cacheSize * 2);

                    return (new ReadThroughputBenchmark() { Initialize = c => Init(c) }, new EvictionConfig(EvictIters(cacheSize), evictSamples, maxThreads), cacheSize);
            }

            throw new InvalidOperationException();
        }

        private static int EvictIters(int cacheSize) => cacheSize switch
        { 
            < 500 => 400,
            < 5000 => 200,
            < 10_000 => 100,
            < 100_000 => 50,
            < 1_000_000 => 25,
            < 10_000_000 => 5,
            _ => 1
        };

        private static void Init(ICache<int, int> cache)
        { 
            Parallel.ForEach(Enumerable.Range(0, cache.Policy.Eviction.Value.Capacity).Select(i => -i), i =>
            {
                cache.GetOrAdd(i, key => i);
            });
        }
    }
}
