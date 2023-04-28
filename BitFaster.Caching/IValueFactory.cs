
using System;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a cache value factory.
    /// </summary>
    public interface IValueFactory<K, V>
    {
        /// <summary>
        /// Creates a value.
        /// </summary>
        /// <param name="key">The key used to create the value.</param>
        /// <returns>The value created.</returns>
        V Create(K key);
    }

    /// <summary>
    /// A wrapper for a cache value factory delegate.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value</typeparam>
    public struct ValueFactory<K, V> : IValueFactory<K, V>
    {
        private readonly Func<K, V> factory;

        /// <summary>
        /// Initializes a new ValueFactory value.
        /// </summary>
        /// <param name="factory">The factory to wrap.</param>
        public ValueFactory(Func<K, V> factory)
        {
            this.factory = factory;
        }

        ///<inheritdoc/>
        public V Create(K key)
        {
            return this.factory(key);
        }
    }

    /// <summary>
    /// A wrapper for a cache value factory delegate that takes an argument.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="TArg">The type of the factory argument</typeparam>
    /// <typeparam name="V">The type of the cache value</typeparam>
    public struct ValueFactoryArg<K, TArg, V> : IValueFactory<K, V>
    {
        private readonly Func<K, TArg, V> factory;
        private readonly TArg arg;

        /// <summary>
        /// Initializes a new ValueFactoryArg value.
        /// </summary>
        /// <param name="factory">The factory to wrap.</param>
        /// <param name="arg">The argument to pass to the factory.</param>
        public ValueFactoryArg(Func<K, TArg, V> factory, TArg arg)
        {
            this.factory = factory;
            this.arg = arg;
        }

        ///<inheritdoc/>
        public V Create(K key)
        {
            return this.factory(key, arg);
        }
    }

    /// <summary>
    /// Represents an async cache value factory.
    /// </summary>
    public interface IAsyncValueFactory<K, V>
    {
        /// <summary>
        /// Creates a value.
        /// </summary>
        /// <param name="key">The key used to create the value.</param>
        /// <returns>The value created.</returns>
        Task<V> CreateAsync(K key);
    }

    /// <summary>
    /// A wrapper for an async cache value factory delegate.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value</typeparam>
    public struct AsyncValueFactory<K, V> : IAsyncValueFactory<K, V>
    {
        private readonly Func<K, Task<V>> factory;

        /// <summary>
        /// Initializes a new ValueFactoryAsync value.
        /// </summary>
        /// <param name="factory">The factory to wrap.</param>
        public AsyncValueFactory(Func<K, Task<V>> factory)
        {
            this.factory = factory;
        }

        ///<inheritdoc/>
        public Task<V> CreateAsync(K key)
        {
            return this.factory(key);
        }
    }

    /// <summary>
    /// A wrapper for an async cache value factory delegate that takes an argument.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="TArg">The type of the factory argument</typeparam>
    /// <typeparam name="V">The type of the cache value</typeparam>
    public struct AsyncValueFactoryArg<K, TArg, V> : IAsyncValueFactory<K, V>
    {
        private readonly Func<K, TArg, Task<V>> factory;
        private readonly TArg arg;

        /// <summary>
        /// Initializes a new ValueFactoryAsyncArg value.
        /// </summary>
        /// <param name="factory">The factory to wrap.</param>
        /// <param name="arg">The argument to pass to the factory.</param>
        public AsyncValueFactoryArg(Func<K, TArg, Task<V>> factory, TArg arg)
        {
            this.factory = factory;
            this.arg = arg;
        }

        ///<inheritdoc/>
        public Task<V> CreateAsync(K key)
        {
            return this.factory(key, arg);
        }
    }
}
