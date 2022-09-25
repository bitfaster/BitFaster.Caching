using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public interface IThroughputBenchConfig
    {
        int Iterations { get; }

        int Samples { get; }

        int[] GetTestData(int threadId);
    }

    public class ZipfConfig : IThroughputBenchConfig
    {
        private int iterations;
        private int[] samples;

        public ZipfConfig(int iterations, int sampleCount, double s, int n)
        {
            this.iterations = iterations;

            Random random = new Random(666);

            samples = new int[sampleCount];
            Zipf.Samples(random, samples, s, n);
        }

        public int Iterations => iterations;

        public int Samples => samples.Length;

        public int[] GetTestData(int threadId)
        {
            return samples;
        }
    }

    public class EvictionConfig : IThroughputBenchConfig
    {
        private int iterations;

        private int[][] samples;

        const int maxSamples = 10_000_000;

        public EvictionConfig(int iterations, int sampleCount, int threadCount)
        {
            if (sampleCount > 100000)
            {
                throw new ArgumentOutOfRangeException("Sample count too large, will result in overlap");
            }

            this.iterations = iterations;
            samples = new int[threadCount][];

            for (int i = 0; i < threadCount; i++)
            {
                samples[i] = Enumerable.Range(i * 100000, sampleCount).ToArray();
            }
        }

        public int Iterations => iterations;

        public int Samples => samples[0].Length;

        public int[] GetTestData(int threadId)
        {
            return samples[threadId];
        }
    }
}
