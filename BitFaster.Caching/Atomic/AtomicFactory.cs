using System;
using System.Diagnostics;
using System.Threading;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A class that provides simple, lightweight exactly once initialization for values
    /// stored in a cache.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public sealed class AtomicFactory<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicFactory{K, V}"/> class.
        /// </summary>
        public AtomicFactory()
        {
            initializer = new Initializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicFactory{K, V}"/> class with the
        /// specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public AtomicFactory(V value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the value. If <see cref="IsValueCreated"/> is false, calling <see cref="GetValue"/> will force initialization via the <paramref name="valueFactory"/> parameter.
        /// </summary>
        /// <param name="key">The key associated with the value.</param>
        /// <param name="valueFactory">The value factory to use to create the value when it is not initialized.</param>
        /// <returns>The value.</returns>
        public V GetValue(K key, Func<K, V> valueFactory)
        {
            if (initializer == null)
            {
                return value;
            }

            return CreateValue(key, valueFactory);
        }

        public V GetValue<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            if (initializer == null)
            {
                return value;
            }

            return CreateValue(key, valueFactory, factoryArgument);
        }

        /// <summary>
        /// Gets a value indicating whether the value has been initialized.
        /// </summary>
        public bool IsValueCreated => initializer == null;

        /// <summary>
        /// Gets the value if it has been initialized, else default.
        /// </summary>
        public V ValueIfCreated
        {
            get
            {
                if (!IsValueCreated)
                {
                    return default;
                }

                return value;
            }
        }

        private V CreateValue(K key, Func<K, V> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                value = init.CreateValue(key, valueFactory);
                initializer = null;
            }

            return value;
        }

        private V CreateValue<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            var init = initializer;

            if (init != null)
            {
                value = init.CreateValue(key, valueFactory, factoryArgument);
                initializer = null;
            }

            return value;
        }

        private class Initializer
        {
            private readonly object syncLock = new object();
            private bool isInitialized;
            private V value;

            public V CreateValue(K key, Func<K, V> valueFactory)
            {
                if (Volatile.Read(ref isInitialized))
                {
                    return value;
                }

                lock (syncLock)
                {
                    if (Volatile.Read(ref isInitialized))
                    {
                        return value;
                    }

                    value = valueFactory(key);
                    Volatile.Write(ref isInitialized, true);
                    return value;
                }
            }

            public V CreateValue<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
            {
                if (Volatile.Read(ref isInitialized))
                {
                    return value;
                }

                lock (syncLock)
                {
                    if (Volatile.Read(ref isInitialized))
                    {
                        return value;
                    }

                    value = valueFactory(key, factoryArgument);
                    Volatile.Write(ref isInitialized, true);
                    return value;
                }
            }
        }
    }
}
