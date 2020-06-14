using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Benchmarks
{
    [MemoryDiagnoser]
    public class PrimitiveBenchmarks
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);
        LinkedList<int> intList = new LinkedList<int>(new int[] { 1, 2, 3 });
        ConcurrentQueue<int> queue = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
        private int counter = 0;

        [Benchmark(Baseline = true)]
        public void ConcurrentQueueSwapLastToFirst()
        {
            Interlocked.Decrement(ref counter);
            var wasDequeue = queue.TryDequeue(out var result);
            queue.Enqueue(result);
            Interlocked.Increment(ref counter);
        }

        [Benchmark()]
        public void LinkedListSwapFirstToLast()
        {
            var first = intList.First;
            intList.RemoveFirst();
            intList.AddLast(first);
        }

        [Benchmark()]
        public void LinkedListLockSwapFirstToLast()
        {
            lock (this.intList)
            {
                var first = intList.First;
                intList.RemoveFirst();
                intList.AddLast(first);
            }
        }

        [Benchmark()]
        public DateTime DateTimeUtcNow()
        {
            return DateTime.UtcNow;
        }

        [Benchmark()]
        public void DictionaryGetOrAdd()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }
    }
}
