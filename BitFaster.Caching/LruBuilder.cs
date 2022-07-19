using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    // recursive generic base class
    public abstract class LruBuilderBase<K, V, TBuilder, TCacheReturn> where TBuilder : LruBuilderBase<K, V, TBuilder, TCacheReturn>
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

        public abstract TCacheReturn Build();
    }

    public class ConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, ConcurrentLruBuilder<K, V>, ICache<K, V>>
    {
        public ConcurrentLruBuilder()
            : base(new LruInfo<K>())
        {
        }

        internal ConcurrentLruBuilder(LruInfo<K> info)
            : base(info)
        {
        }

        public override ICache<K, V> Build()
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

    // marker interface enables type constraints
    public interface IScoped<T> where T : IDisposable
    { }

    public class ScopedLruBuilder<K, V, W> : LruBuilderBase<K, V, ScopedLruBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLruBuilder<K, W> inner;

        internal ScopedLruBuilder(ConcurrentLruBuilder<K, W> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IScopedCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            ICache<K, Scoped<V>> scopedInnerCache = inner.Build() as ICache<K, Scoped<V>>;

            return new ScopedCache<K, V>(scopedInnerCache);
        }
    }

    public class AtomicLruBuilder<K, V> : LruBuilderBase<K, V, AtomicLruBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomic<K, V>> inner;

        internal AtomicLruBuilder(ConcurrentLruBuilder<K, AsyncAtomic<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override ICache<K, V> Build()
        {
            ICache<K, AsyncAtomic<K, V>> innerCache = inner.Build();

            return new AtomicCacheDecorator<K, V>(innerCache);
        }
    }

    public class ScopedAtomicLruBuilder<K, V, W> : LruBuilderBase<K, V, ScopedAtomicLruBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomic<K, W>> inner;

        internal ScopedAtomicLruBuilder(ConcurrentLruBuilder<K, AsyncAtomic<K, W>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IScopedCache<K, V> Build()
        {
            ICache<K, AsyncAtomic<K, Scoped<V>>> level1 = inner.Build() as ICache<K, AsyncAtomic<K, Scoped<V>>>;
            var level2 = new AtomicCacheDecorator<K, Scoped<V>>(level1);
            return new ScopedCache<K, V>(level2);
        }
    }

    public static class ConcurrentLruBuilderExtensions
    { 
        public static ScopedLruBuilder<K, V, Scoped<V>> WithScopedValues<K, V>(this ConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var scoped = new ConcurrentLruBuilder<K, Scoped<V>>(b.info);
            return new ScopedLruBuilder<K, V, Scoped<V>>(scoped);
        }

        public static AtomicLruBuilder<K, V> WithAtomicCreate<K, V>(this ConcurrentLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AsyncAtomic<K, V>>(b.info);
            return new AtomicLruBuilder<K, V>(a);
        }

        public static ScopedAtomicLruBuilder<K, V, Scoped<V>> WithAtomicCreate<K, V, W>(this ScopedLruBuilder<K, V, W> b) where V : IDisposable where W : IScoped<V>
        {
            var atomicScoped = new ConcurrentLruBuilder<K, AsyncAtomic<K, Scoped<V>>>(b.info);

            return new ScopedAtomicLruBuilder<K, V, Scoped<V>>(atomicScoped);
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
                //.WithAtomicCreate()
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
