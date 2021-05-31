using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // what happens if we completely encapsulate scoped?
    // we can't implement ICache, since return types will now be Lifetime, not T
    public class ScopedCache<K, T> where T : IDisposable
    {
        private readonly ICache<K, Scoped<T>> innerCache;

        public ScopedCache(ICache<K, Scoped<T>> innerCache)
        {
            this.innerCache = innerCache;
        }

        public Lifetime<T> GetOrAdd(K key, Func<K, T> valueFactory)
        {
            // additional alloc
            var scopedFactory = new ScopedFactory(valueFactory);

            while (true)
            {
                var scope = innerCache.GetOrAdd(key, scopedFactory.Create);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public bool TryUpdate(K key, T value)
        { 
            // scoped finializer does not call dispose, so if this fails, discarded new Scoped will not dispose value.
            return this.innerCache.TryUpdate(key, new Scoped<T>(value));
        }

        private class ScopedFactory
        { 
            private Func<K, T> valueFactory;

            public ScopedFactory(Func<K, T> valueFactory)
            { 
                this.valueFactory = valueFactory;   
            }

            public Scoped<T> Create(K key)
            {
                return new Scoped<T>(valueFactory(key));
            }
        }
    }
}
