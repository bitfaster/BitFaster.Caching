using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
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
        public (int, double) Run(int warmup, int runs, int threads, IThroughputBenchConfig config, ICache<long, int> cache)
        {
            var results = new List<double>();

            Initialize?.Invoke(cache);

            for (int i = 0; i < warmup; i++)
            {
                Run(i, threads, config, cache);
            }

            int iterationCounter = 0;
            double effectiveMaxRelativeError = 0.02; // https://github.com/dotnet/BenchmarkDotNet/blob/b4ac9df9f7890ca9669e2b9c8835af35c072a453/src/BenchmarkDotNet/Jobs/AccuracyMode.cs#L11

            OutlierMode outlierMode = OutlierMode.RemoveUpper;
            int maxIters = 80;

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
            return (iterationCounter, finalStats.Mean / oneMillion);
        }

        protected abstract double Run(int iter, int threads, IThroughputBenchConfig config, ICache<long, int> cache);
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

                bool yield = index % Environment.ProcessorCount == 0 && threads > 1;

                for (int i = 0; i < config.Iterations; i++)
                {
                    for (int s = 0; s < samples.Length; s++)
                    {
                        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(cache.GetOrAdd(samples[s], func));
                    }

                    // try to allow memory cache eviction thread to run
                    //if (yield)
                    //{
                    //    Thread.Yield();
                    //}
                }
            }

            var time = ParallelBenchmark.Run(action, threads);
            var throughput = (threads * config.Samples * config.Iterations) / time.TotalSeconds;
            if (false)
            {
#pragma warning disable CS0162 // Unreachable code detected
                Console.WriteLine($"{iter} {Format.Throughput(throughput / 1_000_000.0)} ops/sec");
#pragma warning restore CS0162 // Unreachable code detected
            }

            return throughput;
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
