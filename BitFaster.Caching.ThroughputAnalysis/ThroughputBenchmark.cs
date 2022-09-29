using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public interface IThroughputBenchmark
    {
        double Run(int warmup, int runs, int threads, IThroughputBenchConfig config, ICache<int, int> cache);
    }

    public abstract class ThroughputBenchmarkBase
    {
        public Action<ICache<int, int>> Initialize { get; set; }

        public double Run(int warmup, int runs, int threads, IThroughputBenchConfig config, ICache<int, int> cache)
        {
            double[] results = new double[warmup + runs];

            Initialize?.Invoke(cache);

            for (int i = 0; i < warmup + runs; i++)
            {
                results[i] = Run(threads, config, cache);
            }

            // return million ops/sec
            const int oneMillion = 1_000_000;
            return AverageLast(results, runs) / oneMillion;
        }

        protected abstract double Run(int threads, IThroughputBenchConfig config, ICache<int, int> cache);

        private static double AverageLast(double[] results, int count)
        {
            double result = 0;
            for (int i = results.Length - count; i < results.Length; i++)
            {
                result += results[i];
            }

            return result / count;
        }
    }

    public class ReadThroughputBenchmark : ThroughputBenchmarkBase
    {
        protected override double Run(int threads, IThroughputBenchConfig config, ICache<int, int> cache)
        {
            Action<int> action = index => 
            {
                int[] samples = config.GetTestData(index);
                Func<int, int> func = x => x;

                for (int i = 0; i < config.Iterations; i++)
                {
                    for (int s = 0; s < samples.Length; s++)
                    {
                        cache.GetOrAdd(samples[s], func);
                    }
                }
            };

            var time = ParallelBenchmark.Run(action, threads);

            // throughput = ops/sec
            return (threads * config.Samples * config.Iterations) / time.TotalSeconds;
        }
    }

    public class UpdateThroughputBenchmark : ThroughputBenchmarkBase
    {
        protected override double Run(int threads, IThroughputBenchConfig config, ICache<int, int> cache)
        {
            Action<int> action = index =>
            {
                int[] samples = config.GetTestData(index);

                for (int i = 0; i < config.Iterations; i++)
                {
                    for (int s = 0; s < samples.Length; s++)
                    {
                        cache.AddOrUpdate(samples[s], samples[s]);
                    }
                }
            };

            var time = ParallelBenchmark.Run(action, threads);

            // throughput = ops/sec
            return (threads * config.Samples * config.Iterations) / time.TotalSeconds;
        }
    }
}
