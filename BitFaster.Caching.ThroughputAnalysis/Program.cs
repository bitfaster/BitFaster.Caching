using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Environments;

namespace BitFaster.Caching.ThroughputAnalysis
{
    class Program
    {
        private static readonly int maxThreads = Host.GetAvailableCoreCount() * 2;
        private const int repeatCount = 400;

        static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(maxThreads, maxThreads);

            PrintHostInfo();

            Mode mode = Mode.Read;

            var menu = new EasyConsole.Menu()
                .Add("Read", () => mode = Mode.Read)
                .Add("Read + Write", () => mode = Mode.ReadWrite)
                .Add("Update", () => mode = Mode.Update)
                .Add("Evict", () => mode = Mode.Evict);

            menu.Display();

            Console.WriteLine("Generating input distribution...");

            var (bench, dataConfig, capacity) = ConfigFactory.Create(mode, repeatCount);

            var cachesToTest = new List<ICacheFactory>();
            cachesToTest.Add(new ClassicLruFactory(capacity));
            cachesToTest.Add(new MemoryCacheFactory(capacity));
            cachesToTest.Add(new FastConcurrentLruFactory(capacity));
            cachesToTest.Add(new ConcurrentLruFactory(capacity));
            cachesToTest.Add(new ConcurrentLfuFactory(capacity));

            var exporter = new Exporter(maxThreads);
            exporter.Initialize(cachesToTest);

            Console.WriteLine();
            Console.WriteLine($"Running {mode}...");
            Console.WriteLine();

            foreach (int tc in Enumerable.Range(1, maxThreads).ToArray())
            {
                const int warmup = 3;
                const int runs = 6;

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

            exporter.ExportCsv(mode);

            //ConsoleTable
            //    .From(resultTable)
            //    .Configure(o => o.NumberAlignment = Alignment.Right)
            //    .Write(Format.MarkDown);

            Console.WriteLine("Done.");
        }

        private static void PrintHostInfo()
        {
            var hostinfo = HostEnvironmentInfo.GetCurrent();

            foreach (var segment in hostinfo.ToFormattedString())
            {
                string toPrint = segment;

                // remove benchmark dot net
                if (toPrint.StartsWith("Ben"))
                {
                    toPrint = segment.Substring(segment.IndexOf(',') + 2, segment.Length - segment.IndexOf(',') - 2);
                }

                Console.WriteLine(toPrint);
            }

            Console.WriteLine();
            Console.WriteLine($"Available CPU Count: {Host.GetAvailableCoreCount()}");

            if (Host.GetLogicalCoreCount() > Host.GetAvailableCoreCount())
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;

                Console.WriteLine("WARNING: not all cores available.");
                Console.WriteLine($"DOTNET_Thread_UseAllCpuGroups: {Environment.GetEnvironmentVariable("DOTNET_Thread_UseAllCpuGroups") ?? "Not Set (disabled)"}");

                Console.ResetColor();
            }

            Console.WriteLine();
        }

        private static string FormatThroughput(double thru)
        {
            string dformat = "0.00;-0.00";
            string raw = thru.ToString(dformat);
            return raw.PadLeft(7, ' ');
        }
    }
}
