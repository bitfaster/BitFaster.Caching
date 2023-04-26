
using System;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
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

    public struct ValueFactoryArg<K, TArg, V> : IValueFactory<K, V>
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

    public interface IAsyncValueFactory<K, V>
    {
        Task<V> CreateAsync(K key);
    }

    public struct ValueFactoryAsync<K, V> : IAsyncValueFactory<K, V>
    {
        private Func<K, Task<V>> _factory;

        public ValueFactoryAsync(Func<K, Task<V>> _factory)
        {
            this._factory = _factory;
        }

        public Task<V> CreateAsync(K key)
        {
            return _factory(key);
        }
    }

    public struct ValueFactoryAsyncArg<K, TArg, V> : IAsyncValueFactory<K, V>
    {
        private Func<K, TArg, Task<V>> _factory;
        private TArg arg;

        public ValueFactoryAsyncArg(Func<K, TArg, Task<V>> _factory, TArg arg)
        {
            this._factory = _factory;
            this.arg = arg;
        }

        public Task<V> CreateAsync(K key)
        {
            return _factory(key, arg);
        }
    }
}
