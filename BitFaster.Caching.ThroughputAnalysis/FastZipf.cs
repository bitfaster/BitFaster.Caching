using System;

namespace BitFaster.Caching.ThroughputAnalysis
{
    /// <summary>
    /// Generates an approximate Zipf distribution. Previous method was 20x faster than MathNet.Numerics, but could only generate 250 samples/sec.
    /// This approximate method can generate > 1,000,000 samples/sec.
    /// </summary>
    public class FastZipf
    {
        private static readonly Random srandom = new(666);

        /// <summary>
        /// Generate a zipf distribution.
        /// </summary>
        /// <param name="random">The random number generator to use.</param>
        /// <param name="sampleCount">The number of samples.</param>
        /// <param name="s">The skew. s=0 is a uniform distribution. As s increases, high-rank items become rapidly more likely than the rare low-ranked items.</param>
        /// <param name="n">N: the cardinality. The total number of items.</param>
        /// <returns>A zipf distribution.</returns>
        public static long[] Generate(Random random, int sampleCount, double s, int n)
        {
            ZipfRejectionSampler sampler = new ZipfRejectionSampler(random, n, s);

            long[] samples = new long[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = sampler.Sample();
            }

            return samples;
        }

        /// <summary>
        /// Generate a zipf distribution.
        /// </summary>
        /// <param name="sampleCount">The number of samples.</param>
        /// <param name="s">The skew. s=0 is a uniform distribution. As s increases, high-rank items become rapidly more likely than the rare low-ranked items.</param>
        /// <param name="n">N: the cardinality. The total number of items.</param>
        /// <returns>A zipf distribution.</returns>
        public static long[] Generate(int sampleCount, double s, int n)
        {
            return Generate(srandom, sampleCount, s, n);
        }
    }

    // https://jasoncrease.medium.com/rejection-sampling-the-zipf-distribution-6b359792cffa
    public class ZipfRejectionSampler
    {
        private readonly Random _rand;
        private readonly double _skew;
        private readonly double _t;

        public ZipfRejectionSampler(Random random, long N, double skew)
        {
            _rand = random;
            _skew = skew;
            _t = (Math.Pow(N, 1 - skew) - skew) / (1 - skew);
        }

        public long Sample()
        {
            while (true)
            {
                double invB = bInvCdf(_rand.NextDouble());
                long sampleX = (long)(invB + 1);
                double yRand = _rand.NextDouble();
                double ratioTop = Math.Pow(sampleX, -_skew);
                double ratioBottom = sampleX <= 1 ? 1 / _t : Math.Pow(invB, -_skew) / _t;
                double rat = (ratioTop) / (ratioBottom * _t);

                if (yRand < rat)
                    return sampleX;
            }
        }
        private double bInvCdf(double p)
        {
            if (p * _t <= 1)
                return p * _t;
            else
                return Math.Pow((p * _t) * (1 - _skew) + _skew, 1 / (1 - _skew));
        }
    }
}
