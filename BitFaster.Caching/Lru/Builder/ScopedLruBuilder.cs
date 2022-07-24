using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public sealed class ScopedLruBuilder<K, V, W> : LruBuilderBase<K, V, ScopedLruBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLruBuilder<K, W> inner;

        internal ScopedLruBuilder(ConcurrentLruBuilder<K, W> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as ICache<K, Scoped<V>>;

            return new ScopedCache<K, V>(scopedInnerCache);
        }
    }

    public sealed class ScopedAsyncLruBuilder<K, V> : LruBuilderBase<K, V, ScopedAsyncLruBuilder<K, V>, IScopedAsyncCache<K, V>> where V : IDisposable
    {
        private readonly AsyncConcurrentLruBuilder<K, Scoped<V>> inner;

        internal ScopedAsyncLruBuilder(AsyncConcurrentLruBuilder<K, Scoped<V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedAsyncCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as IAsyncCache<K, Scoped<V>>;

            return new ScopedAsyncCache<K, V>(scopedInnerCache);
        }
    }

    public sealed class ScopedAsyncAtomicLruBuilder<K, V> : LruBuilderBase<K, V, ScopedAsyncAtomicLruBuilder<K, V>, IScopedAsyncCache<K, V>> where V : IDisposable
    {
        private readonly AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner;

        internal ScopedAsyncAtomicLruBuilder(AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedAsyncCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as ICache<K, ScopedAsyncAtomicFactory<K, V>>;

            return new AtomicFactoryScopedAsyncCache<K, V>(scopedInnerCache);
        }
    }
}
