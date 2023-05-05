using System;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics;

namespace BitFaster.Caching.ThroughputAnalysis
{
    // produces the same output as MathNet.Numerics Zipf.Samples(random, samples[], s, n)
    // but about 20x faster.
    public class FastZipf
    {
        static Random srandom = new Random(666);

        public static int[] Generate(Random random, int sampleCount, double s, int n)
        {
            double[] num = new double[sampleCount];
            int[] samples = new int[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                while (num[i] == 0.0)
                {
                    num[i] = random.NextDouble();
                }
            }

            double num2 = 1.0 / SpecialFunctions.GeneralHarmonic(n, s);

            // Precompute pow for all i, take reciprocal so that we can multiply instead of divide inside the loop
            double[] rpows = new double[n+1];
            for (int i = 0; i <= n; i++)
            {
                rpows[i] = 1.0d / Math.Pow(i, s);
            }

            Parallel.ForEach(Enumerable.Range(0, samples.Length), (x, j) =>
            {
                double num3 = 0.0;
                int i;

                for (i = 1; i <= n; i++)
                {
                    num3 += num2 * rpows[i]; // Math.Pow(i, s);
                    if (num3 >= num[x])
                    {
                        break;
                    }
                }

                samples[x] = i;
            });

            return samples;
        }

        public static int[] Generate(int sampleCount, double s, int n)
        {
            return Generate(srandom, sampleCount, s, n);
        }
    }
}
