using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    // recursive generic base class
    public abstract class LruBuilderBase<K, V, TBuilder> where TBuilder : LruBuilderBase<K, V, TBuilder>
    {
        internal readonly LruInfo<K> info;

        public LruBuilderBase(LruInfo<K> info)
        {
            this.info = info;
        }

        public TBuilder WithCapacity(int capacity)
        {
            this.info.capacity = capacity;
            return this as TBuilder;
        }

        public TBuilder WithConcurrencyLevel(int concurrencyLevel)
        {
            this.info.concurrencyLevel = concurrencyLevel;
            return this as TBuilder;
        }

        public TBuilder WithKeyComparer(IEqualityComparer<K> comparer)
        {
            this.info.comparer = comparer;
            return this as TBuilder;
        }

        public TBuilder WithMetrics()
        {
            this.info.withMetrics = true;
            return this as TBuilder;
        }

        public TBuilder WithAbosluteExpiry(TimeSpan expiration)
        {
            this.info.expiration = expiration;
            return this as TBuilder;
        }

        public virtual ICache<K, V> Build()
        {
            if (this.info.expiration.HasValue)
            {
                return info.withMetrics ?
                    new ConcurrentTLru<K, V>(this.info.concurrencyLevel, this.info.capacity, this.info.comparer, this.info.expiration.Value)
                    : new FastConcurrentTLru<K, V>(this.info.concurrencyLevel, this.info.capacity, this.info.comparer, this.info.expiration.Value) as ICache<K, V>;
            }

            return info.withMetrics ?
                new ConcurrentLru<K, V>(this.info.concurrencyLevel, this.info.capacity, this.info.comparer)
                : new FastConcurrentLru<K, V>(this.info.concurrencyLevel, this.info.capacity, this.info.comparer) as ICache<K, V>;
        }
    }
    public class ConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, ConcurrentLruBuilder<K, V>>
    {
        public ConcurrentLruBuilder()
            : base(new LruInfo<K>())
        {
        }

        internal ConcurrentLruBuilder(LruInfo<K> info)
            : base(info)
        {
        }
    }

    public static class ConcurrentLruBuilderExtensions
    { 
        public static ConcurrentLruBuilder<K, Scoped<V>> WithScopedValues<K, V>(this ConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            return new ConcurrentLruBuilder<K, Scoped<V>>(b.info);
        }

        public static ConcurrentLruBuilder<K, AsyncAtomic<K, V>> WithAtomicCreate<K, V>(this ConcurrentLruBuilder<K, V> b)
        {
            return new ConcurrentLruBuilder<K, AsyncAtomic<K, V>>(b.info);
        }
    }

    public class LruInfo<K>
    {
        public int capacity = 128;
        public int concurrencyLevel = Defaults.ConcurrencyLevel;
        public TimeSpan? expiration = null;
        public bool withMetrics = false;
        public IEqualityComparer<K> comparer = EqualityComparer<K>.Default;
    }

    public class Test
    {
        public void ScopedPOC()
        {
            // Choose from 16 combinations of Lru/TLru, Instrumented/NotInstrumented, Atomic create/not atomic create, scoped/not scoped

            // layer 1: can choose ConcurrentLru/TLru, FastConcurrentLru/FastConcurrentTLru 
            var c = new ConcurrentLru<int, AsyncAtomic<int, Scoped<Disposable>>>(3);

            // layer 2: optional atomic creation
            var atomic = new AtomicCacheDecorator<int, Scoped<Disposable>>(c);

            // layer 3: optional scoping
            IScopedCache<int, Disposable> scoped = new ScopedCache<int, Disposable>(atomic);

            using (var lifetime = scoped.ScopedGetOrAdd(1, k => new Scoped<Disposable>(new Disposable())))
            {
                var d = lifetime.Value;
            }

            // This builds the correct layer 1 type, but it has not been wrapped with AtomicCacheDecorator or ScopedCache
            var lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();
        }

        public class Disposable : IDisposable
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }
    }
}
