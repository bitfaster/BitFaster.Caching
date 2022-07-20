using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru.Builder
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
}
