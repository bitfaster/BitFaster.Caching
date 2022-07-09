using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    public class LruBuilder<K, V>
    {
        protected readonly Spec spec;

        public LruBuilder()
        { 
            this.spec = new Spec();
        }

        protected LruBuilder(Spec spec)
        {
            this.spec = spec;
        }   

        public LruBuilder<K, V> WithCapacity(int capacity)
        {
            this.spec.capacity = capacity;
            return this;
        }

        public LruBuilder<K, V> WithConcurrencyLevel(int concurrencyLevel)
        {
            this.spec.concurrencyLevel = concurrencyLevel;
            return this;
        }

        public LruBuilder<K, V> WithExpiration(TimeSpan expiration)
        {
            this.spec.expiration = expiration;
            return this;
        }

        public LruBuilder<K, V> WithInstrumentation()
        {
            this.spec.withInstrumentation = true;
            return this;
        }

        public AtomicLruBuilder<K, V> WithAtomicCreate()
        {
            return new AtomicLruBuilder<K, V>(this.spec);
        }

        public virtual ICache<K, V> Build()
        {
            if (this.spec.expiration.HasValue)
            {
                return spec.withInstrumentation ?
                    new ConcurrentTLru<K, V>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer, this.spec.expiration.Value)
                    : new FastConcurrentTLru<K, V>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer, this.spec.expiration.Value) as ICache<K, V>;
            }

            return spec.withInstrumentation ?
                new ConcurrentLru<K, V>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer)
                : new FastConcurrentLru<K, V>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer) as ICache<K, V>;
        }

        public class Spec
        {
            public int capacity = 128;
            public int concurrencyLevel = Defaults.ConcurrencyLevel;
            public TimeSpan? expiration = null;
            public bool withInstrumentation = false;
            public IEqualityComparer<K> comparer = EqualityComparer<K>.Default;
        }
    }

    public class AtomicLruBuilder<K, V> : LruBuilder<K, V>
    {
        public AtomicLruBuilder(Spec spec)
            : base(spec)
        { 
        }

        public override ICache<K, V> Build()
        {
            ICache<K, AsyncAtomic<K, V>> ret = null;

            if (this.spec.expiration.HasValue)
            {
                ret = spec.withInstrumentation ?
                    new ConcurrentTLru<K, AsyncAtomic<K, V>>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer, this.spec.expiration.Value)
                    : new FastConcurrentTLru<K, AsyncAtomic<K, V>>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer, this.spec.expiration.Value) as ICache<K, AsyncAtomic<K, V>>;
            }
            else
            {
                ret = spec.withInstrumentation ?
                    new ConcurrentLru<K, AsyncAtomic<K, V>>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer)
                    : new FastConcurrentLru<K, AsyncAtomic<K, V>>(this.spec.concurrencyLevel, this.spec.capacity, this.spec.comparer) as ICache<K, AsyncAtomic<K, V>>;
            }

            return new AtomicCacheDecorator<K, V>(ret);
        }
    }

    public class Test
    {
        public void T()
        {
            var cache = new LruBuilder<int, int>()
                .WithAtomicCreate()
                .WithInstrumentation()
                .Build();
        
        }
    }
}
