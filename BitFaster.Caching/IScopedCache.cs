using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public interface IScopedCache<K, T> where T : IDisposable
    {
        bool TryGet(K key, out Lifetime<T> value);

        Lifetime<T> GetOrAdd(K key, Func<K, Scoped<T>> valueFactory);

        Task<Lifetime<T>> GetOrAddAsync(K key, Func<K, Task<Scoped<T>>> valueFactory);

        bool TryRemove(K key);

        bool TryUpdate(K key, T value);

        void AddOrUpdate(K key, T value);
    }
}
