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

        public static (ThroughputBenchmarkBase, IThroughputBenchConfig, int) Create(Mode mode, int cacheSize, int maxThreads)
        {
            int iterations = GetIterationCount(cacheSize);
            int samples = GetSampleCount(cacheSize);
            int n = cacheSize; // number of unique items for Zipf

            switch (mode)
            {
                case Mode.Read:
                    return (new ReadThroughputBenchmark(), new ZipfConfig(iterations, samples, s, n), cacheSize);
                case Mode.ReadWrite:
                    // cache holds 10% of all items
                    cacheSize /= 10;
                    return (new ReadThroughputBenchmark(), new ZipfConfig(iterations, samples, s, n), cacheSize);
                case Mode.Update:
                    return (new UpdateThroughputBenchmark(), new ZipfConfig(iterations, samples, s, n), cacheSize);
                case Mode.Evict:
                    return (new ReadThroughputBenchmark() { Initialize = c => EvictionInit(c) }, new EvictionConfig(iterations, samples, maxThreads), cacheSize);
            }

            throw new InvalidOperationException();
        }

        private static int GetIterationCount(int cacheSize) => cacheSize switch
        {
            < 500 => 400,
            < 5_000 => 200,
            < 10_000 => 100,
            < 100_000 => 50,
            < 1_000_000 => 25,
            < 10_000_000 => 5,
            _ => 1
        };

        private static int GetSampleCount(int cacheSize) => cacheSize switch
        {
            < 5_000 => cacheSize * 4,
            < 5_000_000 => cacheSize * 2,
            _ => cacheSize
        };

        private static void EvictionInit(ICache<long, int> cache)
        {
            Parallel.ForEach(Enumerable.Range(0, cache.Policy.Eviction.Value.Capacity).Select(i => -i), i =>
            {
                cache.GetOrAdd(i, key => i);
            });
        }
    }
}
