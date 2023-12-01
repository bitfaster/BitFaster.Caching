using System;
using System.Linq;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public interface IThroughputBenchConfig
    {
        int Iterations { get; set; }

        int Samples { get; }

        long[] GetTestData(int threadId);
    }

    public class ZipfConfig : IThroughputBenchConfig
    {
        private int iterations;
        private readonly long[] samples;

        public ZipfConfig(int sampleCount, double s, int n)
        {
            samples = FastZipf.Generate(sampleCount, s, n);
        }

        public int Iterations { get => iterations; set => iterations = value; }

        public int Samples => samples.Length;

        public long[] GetTestData(int threadId)
        {
            return samples;
        }
    }

    public class EvictionConfig : IThroughputBenchConfig
    {
        private int iterations;

        private readonly long[][] samples;

        const int maxSamples = 10_000_000;

        public EvictionConfig(int sampleCount, int threadCount)
        {
            if (sampleCount > maxSamples)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "Sample count too large, will result in overlap");
            }

            samples = new long[threadCount][];

            Parallel.ForEach(Enumerable.Range(0, threadCount), i =>
            {
                samples[i] = Enumerable.Range(i * maxSamples, sampleCount).Select(i => (long)i).ToArray();
            });
        }

        public int Iterations { get => iterations; set => iterations = value; }

        public int Samples => samples[0].Length;

        public long[] GetTestData(int threadId)
        {
            return samples[threadId];
        }
    }
}
