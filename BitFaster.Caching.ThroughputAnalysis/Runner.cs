using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class Runner
    {
        private static readonly int maxThreads = Host.GetAvailableCoreCount() * 2;

        public static void Run(Mode mode, int cacheSize)
        {
            ThreadPool.SetMinThreads(maxThreads, maxThreads);

            foreach (Mode value in Enum.GetValues(mode.GetType()))
            {
                if (mode.HasFlag(value) && value != Mode.All)
                {
                    RunTest(value, cacheSize);
                }
            }           
        }

        private static void RunTest(Mode mode, int cacheSize)
        {
            Console.WriteLine();
            Console.WriteLine("Generating input distribution...");

            var (bench, dataConfig, capacity) = ConfigFactory.Create(mode, cacheSize, maxThreads);

            var cachesToTest = new List<ICacheFactory>();
            cachesToTest.Add(new ClassicLruFactory(capacity));
            cachesToTest.Add(new MemoryCacheFactory(capacity));
            cachesToTest.Add(new FastConcurrentLruFactory(capacity));
            cachesToTest.Add(new ConcurrentLruFactory(capacity));
            cachesToTest.Add(new ConcurrentLfuFactory(capacity));

            var exporter = new Exporter(maxThreads);
            exporter.Initialize(cachesToTest);

            Console.WriteLine();
            Console.WriteLine($"Running {mode} with size {capacity} over {maxThreads} threads...");
            Console.WriteLine();

            foreach (int tc in Enumerable.Range(1, maxThreads).ToArray())
            {
                const int warmup = 3;
                const int runs = 6;

                UpdateTitle(mode, tc, maxThreads);

                foreach (var cacheConfig in cachesToTest)
                {
                    var (sched, cache) = cacheConfig.Create(tc);
                    double thru = bench.Run(warmup, runs, tc, dataConfig, cache);
                    (sched as IDisposable)?.Dispose();

                    cacheConfig.DataRow[tc.ToString()] = thru.ToString();
                    Console.WriteLine($"{cacheConfig.Name} ({tc.ToString("00")}) {FormatThroughput(thru)} million ops/sec");
                }
            }

            exporter.CaptureRows(cachesToTest);

            exporter.ExportCsv(mode, cacheSize);

            //ConsoleTable
            //    .From(resultTable)
            //    .Configure(o => o.NumberAlignment = Alignment.Right)
            //    .Write(Format.MarkDown);
        }

        private static string FormatThroughput(double thru)
        {
            string dformat = "0.00;-0.00";
            string raw = thru.ToString(dformat);
            return raw.PadLeft(7, ' ');
        }

        private static void UpdateTitle(Mode mode, int tc, int maxTc)
        {
            Console.Title = $"{mode} {tc}/{maxTc}";
        }
    }
}
