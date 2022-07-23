using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public class ScopedAtomicLruBuilder<K, V> : LruBuilderBase<K, V, ScopedAtomicLruBuilder<K, V>, IScopedCache<K, V>> where V : IDisposable
    {
        private readonly ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>> inner;

        internal ScopedAtomicLruBuilder(ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IScopedCache<K, V> Build()
        {
            var level1 = inner.Build() as ICache<K, ScopedAtomicFactory<K, V>>;
            return new AtomicFactoryScopedCache<K, V>(level1);
        }
    }

    public class ScopedAsyncAtomicLruBuilder<K, V> : LruBuilderBase<K, V, ScopedAsyncAtomicLruBuilder<K, V>, IScopedCache<K, V>> where V : IDisposable
    {
        private readonly ConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner;

        internal ScopedAsyncAtomicLruBuilder(ConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IScopedCache<K, V> Build()
        {
            var level1 = inner.Build() as ICache<K, ScopedAsyncAtomicFactory<K, V>>;
            return new AtomicFactoryScopedAsyncCache<K, V>(level1);
        }
    }
}
