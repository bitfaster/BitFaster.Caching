using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching
{
    public interface ICache<K, V>
    {
        bool TryGet(K key, out V value);

        V GetOrAdd(K key, Func<K, V> valueFactory);

        Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory);

        bool TryRemove(K key);
    }
}
