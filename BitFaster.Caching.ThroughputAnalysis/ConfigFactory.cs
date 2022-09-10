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
                    return (new ReadThroughputBenchmark(), new EvictionConfig(repeatCount, sampleCount, maxThreads), n);
            }

            throw new InvalidOperationException();
        }
    }
}
