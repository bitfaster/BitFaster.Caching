using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Perfolizer.Mathematics.OutlierDetection;

namespace BitFaster.Caching.ThroughputAnalysis
{
    // This is taken from BenchmarkDotNet:
    // https://github.com/dotnet/BenchmarkDotNet/blob/b4ac9df9f7890ca9669e2b9c8835af35c072a453/src/BenchmarkDotNet/Engines/DeadCodeEliminationHelper.cs#L6
    public static class DeadCodeEliminationHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void KeepAliveWithoutBoxing<T>(T _) { }
    }

    public interface IThroughputBenchmark
    {
        double Run(int warmup, int runs, int threads, IThroughputBenchConfig config, ICache<int, int> cache);
    }

    public abstract class ThroughputBenchmarkBase
    {
        public Action<ICache<long, int>> Initialize { get; set; }

        // https://github.com/dotnet/BenchmarkDotNet/blob/b4ac9df9f7890ca9669e2b9c8835af35c072a453/src/BenchmarkDotNet/Engines/EngineGeneralStage.cs#L18
        public double Run(int warmup, int runs, int threads, IThroughputBenchConfig config, ICache<long, int> cache)
        {
            var results = new List<double>();

            Initialize?.Invoke(cache);

            for (int i = 0; i < warmup; i++)
            {
                Run(i, threads, config, cache);
            }

            int iterationCounter = 0;
            double effectiveMaxRelativeError = 0.04; // https://github.com/dotnet/BenchmarkDotNet/blob/b4ac9df9f7890ca9669e2b9c8835af35c072a453/src/BenchmarkDotNet/Jobs/AccuracyMode.cs#L11

            OutlierMode outlierMode = OutlierMode.RemoveUpper;
            int maxIters = 25;

            while (true)
            {
                iterationCounter++;
                results.Add(Run(iterationCounter, threads, config, cache));
                var statistics = MeasurementsStatistics.Calculate(results, outlierMode);
                double actualError = statistics.ConfidenceInterval.Margin;

                double maxError1 = effectiveMaxRelativeError * statistics.Mean;
                double maxError2 = double.MaxValue;
                double maxError = Math.Min(maxError1, maxError2);

                if (iterationCounter >= runs && actualError < maxError)
                    break;

                if (iterationCounter >= maxIters)
                    break;
            }

            var finalStats = MeasurementsStatistics.Calculate(results, outlierMode);

            // return million ops/sec
            const int oneMillion = 1_000_000;
            return finalStats.Mean / oneMillion;
        }

        protected abstract double Run(int iter, int threads, IThroughputBenchConfig config, ICache<long, int> cache);

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
        protected override double Run(int iter,int threads, IThroughputBenchConfig config, ICache<long, int> cache)
        {
            [MethodImpl(BenchmarkDotNet.Portability.CodeGenHelper.AggressiveOptimizationOption)]
            void action(int index)
            {
                long[] samples = config.GetTestData(index);
                int func(long x) => (int)x;

                for (int i = 0; i < config.Iterations; i++)
                {
                    for (int s = 0; s < samples.Length; s++)
                    {
                        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(cache.GetOrAdd(samples[s], func));
                    }
                }
            }

            var time = ParallelBenchmark.Run(action, threads);

            // throughput = ops/sec
            var throughput = (threads * config.Samples * config.Iterations) / time.TotalSeconds;

       //     if (throughput / 1_000_000 > 500)
            {
             //   Console.WriteLine($"{iter} {FormatThroughput(throughput / 1_000_000.0)} ops/sec");

            }

            return throughput;
        }

        private static string FormatThroughput(double thru)
        {
            string dformat = "0.00;-0.00";
            string raw = thru.ToString(dformat);
            return raw.PadLeft(7, ' ');
        }
    }


    public class UpdateThroughputBenchmark : ThroughputBenchmarkBase
    {
        protected override double Run(int iter,int threads, IThroughputBenchConfig config, ICache<long, int> cache)
        {
            [MethodImpl(BenchmarkDotNet.Portability.CodeGenHelper.AggressiveOptimizationOption)]
            void action(int index)
            {
                long[] samples = config.GetTestData(index);

                for (int i = 0; i < config.Iterations; i++)
                {
                    for (int s = 0; s < samples.Length; s++)
                    {
                        cache.AddOrUpdate(samples[s], (int)samples[s]);
                    }
                }
            }

            var time = ParallelBenchmark.Run(action, threads);

            // throughput = ops/sec
            return (threads * config.Samples * config.Iterations) / time.TotalSeconds;
        }
    }
}
