using System;
using System.Linq;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public interface IThroughputBenchConfig
    {
        int Iterations { get; }

        int Samples { get; }

        long[] GetTestData(int threadId);
    }

    public class ZipfConfig : IThroughputBenchConfig
    {
        private readonly int iterations;
        private readonly long[] samples;

        public ZipfConfig(int iterations, int sampleCount, double s, int n)
        {
            this.iterations = iterations;

            samples = FastZipf.Generate(sampleCount, s, n);
        }

        public int Iterations => iterations;

        public int Samples => samples.Length;

        public long[] GetTestData(int threadId)
        {
            return samples;
        }
    }

    public class EvictionConfig : IThroughputBenchConfig
    {
        private readonly int iterations;

        private readonly long[][] samples;

        const int maxSamples = 10_000_000;

        public EvictionConfig(int iterations, int sampleCount, int threadCount)
        {
            if (sampleCount > maxSamples)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "Sample count too large, will result in overlap");
            }

            this.iterations = iterations;
            samples = new long[threadCount][];

            Parallel.ForEach(Enumerable.Range(0, threadCount), i =>
            {
                samples[i] = Enumerable.Range(i * maxSamples, sampleCount).Select(i => (long)i).ToArray();
            });
        }

        public int Iterations => iterations;

        public int Samples => samples[0].Length;

        public long[] GetTestData(int threadId)
        {
            return samples[threadId];
        }
    }
}
