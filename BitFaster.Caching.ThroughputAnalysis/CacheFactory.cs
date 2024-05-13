
using System;
using System.Collections.Generic;
using System.Data;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public interface ICacheFactory
    {
        (IScheduler, ICache<long, int>) Create(int threadCount);

        public string Name { get; }

        DataRow DataRow { get; set; }
    }

    public class FastConcurrentLruFactory : ICacheFactory
    {
        private readonly int capacity;

        public FastConcurrentLruFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "FastConcurrentLru";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<long, int>) Create(int threadCount)
        {
            var cache = new FastConcurrentLru<long, int>(threadCount, capacity, EqualityComparer<long>.Default);

            return (null, cache);
        }
    }

    public class ConcurrentLruFactory : ICacheFactory
    {
        private readonly int capacity;

        public ConcurrentLruFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ConcurrentLru";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<long, int>) Create(int threadCount)
        {
            var cache = new ConcurrentLru<long, int>(threadCount, capacity, EqualityComparer<long>.Default);

            return (null, cache);
        }
    }

    public class MemoryCacheFactory : ICacheFactory
    {
        private readonly int capacity;

        public MemoryCacheFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "MemoryCache";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<long, int>) Create(int threadCount)
        {
            var cache = new MemoryCacheAdaptor<long, int>(capacity);

            return (null, cache);
        }
    }

    public class ConcurrentLfuFactory : ICacheFactory
    {
        private readonly int capacity;

        public ConcurrentLfuFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ConcurrentLfu";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<long, int>) Create(int threadCount)
        {
            var scheduler = new BackgroundThreadScheduler();
            var cache = new ConcurrentLfu<long, int>(
                concurrencyLevel: threadCount, 
                capacity: capacity, 
                scheduler: scheduler, 
                EqualityComparer<long>.Default);

            return (scheduler, cache);
        }
    }

    public class ConcurrentTLfuFactory : ICacheFactory
    {
        private readonly int capacity;

        public ConcurrentTLfuFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ConcurrentTLfu";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<long, int>) Create(int threadCount)
        {
            var scheduler = new BackgroundThreadScheduler();

            var cache = new ConcurrentLfuBuilder<long, int>()
                .WithCapacity(capacity)
                .WithScheduler(scheduler)
                .WithConcurrencyLevel(threadCount)
                .WithKeyComparer(EqualityComparer<long>.Default)
                .WithExpireAfterWrite(TimeSpan.FromHours(1))
                .Build();

            return (scheduler, cache);
        }
    }

    public class ClassicLruFactory : ICacheFactory
    {
        private readonly int capacity;

        public ClassicLruFactory(int capacity)
        {
            this.capacity = capacity;
        }

        public string Name => "ClassicLru";

        public DataRow DataRow { get; set; }

        public (IScheduler, ICache<long, int>) Create(int threadCount)
        {
            var cache = new ClassicLru<long, int>(threadCount, capacity, EqualityComparer<long>.Default);

            return (null, cache);
        }
    }
}
