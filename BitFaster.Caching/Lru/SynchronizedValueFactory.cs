using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public class SynchronizedValueFactory<K, V>
    {
        private readonly ICache<K, V> cache;
        private readonly Func<K, V> valueFactory;
        private readonly Func<K, Task<V>> valueFactoryAsync;
        private SingletonCache<K, SemaphoreSlim> semaphoreCache = new SingletonCache<K, SemaphoreSlim>();

        public SynchronizedValueFactory(ICache<K, V> cache, Func<K, V> valueFactory, Func<K, Task<V>> valueFactoryAsync)
        { 
            this.cache = cache;
            this.valueFactory = valueFactory;
            this.valueFactoryAsync = valueFactoryAsync;
        }

        public SynchronizedValueFactory(ICache<K, V> cache,  Func<K, V> valueFactory)
        { 
            this.cache = cache;
            this.valueFactory = valueFactory;
            this.valueFactoryAsync = k => Task.FromResult(valueFactory(k));
        }

        public SynchronizedValueFactory(ICache<K, V> cache, Func<K, Task<V>> valueFactoryAsync)
        { 
            this.cache = cache;
            this.valueFactory = k => valueFactoryAsync(k).GetAwaiter().GetResult();
            this.valueFactoryAsync = k => Task.FromResult(valueFactory(k));
        }

        public V Create(K key)
        {
            using (var sema = this.semaphoreCache.Acquire(key, k => new SemaphoreSlim(1, 1)))
            {
                sema.Value.Wait();

                try
                {
                    return this.cache.GetOrAdd(key, this.valueFactory);
                }
                finally
                {
                    sema.Value.Release();
                }
            }
        }

        public async Task<V> CreateAsync(K key)
        { 
            using (var sema = this.semaphoreCache.Acquire(key, k => new SemaphoreSlim(1, 1)))
            { 
                await sema.Value.WaitAsync();

                try
                { 
                    return await this.cache.GetOrAddAsync(key, this.valueFactoryAsync);
                }
                finally
                { 
                   sema.Value.Release();
                }
            }
        }
    }

    public class Example
    { 
        public void Test()
        { 
var lru = new ConcurrentLru<int, string>(9);
Func<int, string> valueFactory = x => x.ToString();
var synchronized = new SynchronizedValueFactory<int, string>(lru, valueFactory);

var res = lru.GetOrAdd(1, k => synchronized.Create(k));
        }
    }
}
