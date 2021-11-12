using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;
using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Text;

namespace BitFaster.Caching.Benchmarks.Lru
{
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    public class LruMultiGet
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTLru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        private static readonly string[] stringKeys = new string[6];

        [GlobalSetup]
        public void GlobalSetup()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 6; i++)
            {
                dictionary.GetOrAdd(1, func);

                concurrentLru.GetOrAdd(i, func);
                fastConcurrentLru.GetOrAdd(i, func);
                concurrentTLru.GetOrAdd(i, func);
                fastConcurrentTLru.GetOrAdd(i, func);

                classicLru.GetOrAdd(i, func);
                stringKeys[i] = i.ToString();
                memoryCache.Set(stringKeys[i], "test", new CacheItemPolicy());
            }
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = 24)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            { 
                for (int i = 0; i < 6; i++)
                {
                    dictionary.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    fastConcurrentLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    concurrentLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    fastConcurrentTLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    concurrentTLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    classicLru.GetOrAdd(1, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void MemoryCache()
        {
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    memoryCache.Get(stringKeys[i]);
                }
            }
        }
    }
}
