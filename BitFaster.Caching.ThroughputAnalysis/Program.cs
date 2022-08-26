using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using ConsoleTables;
using CsvHelper;
using MathNet.Numerics.Distributions;

namespace BitFaster.Caching.ThroughputAnalysis
{
    class Program
    {
        const double s = 0.86;
        const int n = 500;
        static int capacity = 500;
        const int maxThreads = 52;
        const int sampleCount = 2000;
        const int repeatCount = 400;

        private static int[] samples = new int[sampleCount];

        static void Main(string[] args)
        {
            ThreadPool.SetMaxThreads(maxThreads, maxThreads);

            var menu = new EasyConsole.Menu()
                .Add("Read", () => capacity = n)
                .Add("Read + Write", () => capacity = n / 10);

            menu.Display();

            Console.WriteLine("Generating input distribution...");
            samples = new int[sampleCount];
            Zipf.Samples(samples, s, n);

            int[] threadCount = Enumerable.Range(1, maxThreads).ToArray();

            // Desired output:
            // Class       1  2  3  4  5
            // Classic       5  6  7  7  8
            // Concurrent    5  6  7  7  8
            DataTable resultTable = new DataTable();
            resultTable.Clear();
            resultTable.Columns.Add("Class");
            foreach (var tc in threadCount)
            {
                resultTable.Columns.Add(tc.ToString());
            }

            
            DataRow classicLru = resultTable.NewRow();
            DataRow concurrentLru = resultTable.NewRow();
            DataRow concurrentLfu = resultTable.NewRow();

            classicLru["Class"] = "classicLru";
            concurrentLru["Class"] = "concurrentLru";
            concurrentLfu["Class"] = "concurrentLfu";

            foreach (int tc in threadCount)
            {
                const int warmup = 3;
                const int runs = 6;
                double[] results = new double[warmup + runs];

                //for (int i = 0; i < warmup + runs; i++)
                //{
                //    results[i] = MeasureThroughput(new ClassicLru<int, int>(tc, capacity, EqualityComparer<int>.Default), tc);
                //}
                //double avg = AverageLast(results, runs) / 1000000;
                //Console.WriteLine($"ClassicLru ({tc}) {avg} million ops/sec");
                //classicLru[tc.ToString()] = avg.ToString();

                //for (int i = 0; i < warmup + runs; i++)
                //{
                //    results[i] = MeasureThroughput(new ConcurrentLru<int, int>(tc, capacity, EqualityComparer<int>.Default), tc);
                //}
                //avg = AverageLast(results, runs) / 1000000;
                //Console.WriteLine($"ConcurrLru ({tc}) {avg} million ops/sec");
                //concurrentLru[tc.ToString()] = avg.ToString();

                for (int i = 0; i < warmup + runs; i++)
                {
                    var scheduler = new BackgroundThreadScheduler();
                    results[i] = MeasureThroughput(new ConcurrentLfu<int, int>(concurrencyLevel: tc, capacity: capacity, scheduler: scheduler), tc);
                    scheduler.Dispose();
                }
                var avg = AverageLast(results, runs) / 1000000;
                Console.WriteLine($"ConcurrLfu ({tc}) {avg} million ops/sec");
                concurrentLfu[tc.ToString()] = avg.ToString();
            }

            resultTable.Rows.Add(classicLru);
            resultTable.Rows.Add(concurrentLru);
            resultTable.Rows.Add(concurrentLfu);

            ExportCsv(resultTable);

            //ConsoleTable
            //    .From(resultTable)
            //    .Configure(o => o.NumberAlignment = Alignment.Right)
            //    .Write(Format.MarkDown);

            Console.WriteLine("Done.");
        }

        private static double AverageLast(double[] results, int count)
        {
            double result = 0;
            for (int i = results.Length - count; i < results.Length; i++)
            {
                result += results[i];
            }

            return result / count;
        }

        private static double MeasureThroughput(ICache<int, int> cache, int threadCount)
        {
            var tasks = new Task[threadCount];
            ManualResetEvent mre = new ManualResetEvent(false);

            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() => Test(mre, cache));
            }

            var sw = Stopwatch.StartNew();
            mre.Set();

            Task.WaitAll(tasks);

            sw.Stop();

            // throughput = ops/sec
            return (threadCount * sampleCount * repeatCount) / sw.Elapsed.TotalSeconds;
        }

        private static void Test(ManualResetEvent mre, ICache<int, int> cache)
        {
            // cache has 50 capacity
            // make zipf for 500 total items, 2000 samples
            // each thread will lookup all samples 5 times in a row, for a total of 10k GetOrAdds per thread
            Func<int, int> func = x => x;

            mre.WaitOne();

            for (int j = 0; j < repeatCount; j++)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    cache.GetOrAdd(samples[i], func);
                }
            }
        }

        public static void ExportCsv(DataTable results)
        {
            using (var textWriter = File.CreateText(@"Results.csv"))
            using (var csv = new CsvWriter(textWriter, CultureInfo.InvariantCulture))
            {
                foreach (DataColumn column in results.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                csv.NextRecord();

                foreach (DataRow row in results.Rows)
                {
                    for (var i = 0; i < results.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]);
                    }
                    csv.NextRecord();
                }
            }
        }
    }
}
