using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests
{
    // Goal: allow different forms of ValueFactory delegate without
    // - extra closure/memory allocation
    // - runtime penalty
    // - code duplication
    //
    // Would the following approach be viable? How would this extend to async?
    public class FakeCache<K, V>
    { 
        ConcurrentDictionary<K, V> _cache = new ConcurrentDictionary<K, V>();

        public V GetOrAdd(K key, Func<K, V> factory)
        {
            IValueFactory<K, V> f = new ValueFactory<K, V>(factory);
            return GetOrAdd(key, ref f);
        }

        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> factory, TArg arg)
        {
            IValueFactory<K, V> f = new ValueFactoryArg<K, V, TArg>(factory, arg);
            return GetOrAdd(key, ref f);
        }

        // this represents all the downstream atomic code, which now only understands ref IValueFactory<K, V> end to end
        private V GetOrAdd(K key, ref IValueFactory<K, V> factory)
        {
            return _cache.GetOrAdd(key, factory.Create);
        }
    }

    public interface IValueFactory<K, V>
    {
        V Create(K key);
    }

    public struct ValueFactory<K, V> : IValueFactory<K, V>
    {
        private Func<K, V> _factory;

        public ValueFactory(Func<K, V> _factory)
        {
            this._factory = _factory;
        }

        public V Create(K key)
        {
            return _factory(key);
        }
    }

    public struct ValueFactoryArg<K, V, TArg> : IValueFactory<K, V>
    {
        private Func<K, TArg, V> _factory;
        private TArg arg;

        public ValueFactoryArg(Func<K, TArg, V> _factory, TArg arg)
        {
            this._factory = _factory;
            this.arg = arg;
        }

        public V Create(K key)
        {
            return _factory(key, arg);
        }
    }
}
