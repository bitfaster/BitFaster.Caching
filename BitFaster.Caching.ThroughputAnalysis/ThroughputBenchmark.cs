using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public (int, double) Run(int warmup, int runs, int threads, IThroughputBenchConfig config, ICache<long, int> cache)
        {
            var results = new List<double>();

            Initialize?.Invoke(cache);

            // Warmup a few times before estimating run time
            config.Iterations = 10;

            for (int i = 0; i < warmup; i++)
            {
                Run(i, threads, config, cache);
            }

            // Pilot stage: estimate how many iterations to use to get stable measurements.
            // this gives a run time of about 20 seconds per benchmark with 80 runs per config
            // It can give unstable results if the pilot call returns much slower than the workload calls.
            while (true)
            {
                var sw = Stopwatch.StartNew();
                Run(0, threads, config, cache);

                if (sw.Elapsed > TimeSpan.FromMilliseconds(200))
                {
                    break;
                }

                config.Iterations = (int)(1.2 * config.Iterations);
            }

            int runCounter = 0;
            double effectiveMaxRelativeError = 0.02; // https://github.com/dotnet/BenchmarkDotNet/blob/b4ac9df9f7890ca9669e2b9c8835af35c072a453/src/BenchmarkDotNet/Jobs/AccuracyMode.cs#L11

            OutlierMode outlierMode = OutlierMode.RemoveUpper;
            int maxRuns = 80;

            while (true)
            {
                runCounter++;
                results.Add(Run(runCounter, threads, config, cache));
                var statistics = MeasurementsStatistics.Calculate(results, outlierMode);
                double actualError = statistics.ConfidenceInterval.Margin;

                double maxError1 = effectiveMaxRelativeError * statistics.Mean;
                double maxError2 = double.MaxValue;
                double maxError = Math.Min(maxError1, maxError2);

                if (runCounter >= runs && actualError < maxError)
                    break;

                if (runCounter >= maxRuns)
                    break;

                Console.Write("_");
            }

            Console.WriteLine();

            var finalStats = MeasurementsStatistics.Calculate(results, outlierMode);

            // return million ops/sec
            //const int oneMillion = 1_000_000;
            return (runCounter, finalStats.Mean);
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

                for (int i = 0; i < config.Iterations; i++)
                {
                    for (int s = 0; s < samples.Length; s++)
                    {
                        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(cache.GetOrAdd(samples[s], func));
                    }
                }
            }

            // Avoid dividing a very large number by a very small number. This is a source of
            // inacurracy.
            var time = ParallelBenchmark.Run(action, threads);
            var millionOps = (threads * config.Samples * config.Iterations) / 1_000_000.0;
            var throughput = millionOps / time.TotalSeconds;
            if (false)
            {
#pragma warning disable CS0162 // Unreachable code detected
                Console.WriteLine($"{iter} {Format.Throughput(throughput)} ops/sec");
#pragma warning restore CS0162 // Unreachable code detected
            }

            // throughput = million ops/sec
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

            var millionOps = (threads * config.Samples * config.Iterations) / 1_000_000.0;
            var throughput = millionOps / time.TotalSeconds;

            // throughput = million ops/sec
            return throughput;
        }
    }
}
