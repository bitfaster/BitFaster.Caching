using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class LruCycleBench
    {
        private static readonly ClassicLru<int, int> classicLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        [Benchmark()]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                fastConcurrentLru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                concurrentLru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                fastConcurrentTLru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                concurrentTlru.GetOrAdd(i, func);
        }
    }
}
