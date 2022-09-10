using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public interface ICacheFactory
    {
        (IScheduler, ICache<int, int>) Create(int threadCount);

        public string Name { get; }

        DataRow DataRow { get; set; }
    }

    public class FastConcurrentLruFactory : ICacheFactory
    {
        private int capacity;

        public FastConcurrentLruFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "FsTConcLRU";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<int, int>) Create(int threadCount)
        {
            var cache = new FastConcurrentLru<int, int>(threadCount, capacity, EqualityComparer<int>.Default);

            return (null, cache);
        }
    }

    public class ConcurrentLruFactory : ICacheFactory
    {
        private int capacity;

        public ConcurrentLruFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ConcurrLRU";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<int, int>) Create(int threadCount)
        {
            var cache = new ConcurrentLru<int, int>(threadCount, capacity, EqualityComparer<int>.Default);

            return (null, cache);
        }
    }

    public class MemoryCacheFactory : ICacheFactory
    {
        private int capacity;

        public MemoryCacheFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "MemryCache";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<int, int>) Create(int threadCount)
        {
            var cache = new MemoryCacheAdaptor<int, int>(capacity);

            return (null, cache);
        }
    }

    public class ConcurrentLfuFactory : ICacheFactory
    {
        private int capacity;

        public ConcurrentLfuFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ConcurrLFU";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<int, int>) Create(int threadCount)
        {
            var scheduler = new BackgroundThreadScheduler();
            var cache = new ConcurrentLfu<int, int>(
                concurrencyLevel: threadCount, 
                capacity: capacity, 
                scheduler: scheduler, 
                EqualityComparer<int>.Default);

            return (scheduler, cache);
        }
    }

    public class ClassicLruFactory : ICacheFactory
    {
        private int capacity;

        public ClassicLruFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ClassicLru";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<int, int>) Create(int threadCount)
        {
            var cache = new ClassicLru<int, int>(threadCount, capacity, EqualityComparer<int>.Default);

            return (null, cache);
        }
    }
}
