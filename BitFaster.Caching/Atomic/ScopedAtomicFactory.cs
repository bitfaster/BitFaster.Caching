using System;
using System.Diagnostics;
using System.Threading;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A class that provides simple, lightweight exactly once initialization for scoped values
    /// stored in a cache.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    /// <remarks>
    /// Requirements:
    /// <list type="number">
    ///    <item>
    ///        <description>Exactly once disposal.</description>
    ///    </item>
    ///    <item>
    ///        <term>Exactly once invocation of value factory</term>
    ///        <description>Values are created atomically.</description>
    ///    </item>
    ///    <item>
    ///        <term>Resolve race between create dispose init</term>
    ///        <description>If disposed is called before value is created, scoped value is disposed for life.</description>
    ///    </item>
    ///</list>
    /// </remarks>
    [DebuggerDisplay("IsScopeCreated={initializer == null}, Value={ScopeIfCreated}")]
    public sealed class ScopedAtomicFactory<K, V> : IScoped<V>, IDisposable where V : IDisposable
    {
        private Scoped<V> scope;
        private Initializer initializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopedAtomicFactory{K, V}"/> class.
        /// </summary>
        public ScopedAtomicFactory()
        {
            initializer = new Initializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopedAtomicFactory{K, V}"/> class with the
        /// specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public ScopedAtomicFactory(V value)
        {
            scope = new Scoped<V>(value);
        }

        /// <summary>
        /// Gets a value indicating whether the scope has been initialized.
        /// </summary>
        public bool IsScopeCreated => initializer == null;

        /// <summary>
        /// Gets the scope if it has been initialized, else default.
        /// </summary>
        public Scoped<V> ScopeIfCreated
        {
            get
            {
                if (initializer != null)
                {
                    return default;
                }

                return scope;
            }
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        public bool TryCreateLifetime(out Lifetime<V> lifetime)
        {
            if (scope?.IsDisposed ?? false || initializer != null)
            {
                lifetime = default;
                return false;
            }

            return scope.TryCreateLifetime(out lifetime);
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="key">The key associated with the scoped value.</param>
        /// <param name="valueFactory">The value factory to use to create the scoped value when it is not initialized.</param>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        // backcompat: remove
        public bool TryCreateLifetime(K key, Func<K, Scoped<V>> valueFactory, out Lifetime<V> lifetime)
        {
            // backcompat
            return TryCreateLifetime(key, new ValueFactory<K, Scoped<V>>(valueFactory), out lifetime);
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <typeparam name="TFactory">The type of the value factory.</typeparam>
        /// <param name="key">The key associated with the scoped value.</param>
        /// <param name="valueFactory">The value factory to use to create the scoped value when it is not initialized.</param>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        public bool TryCreateLifetime<TFactory>(K key, TFactory valueFactory, out Lifetime<V> lifetime) where TFactory : struct, IValueFactory<K, Scoped<V>>
        {
            if (scope?.IsDisposed ?? false)
            {
                lifetime = default;
                return false;
            }

            // Create scope EXACTLY once, ref count cas operates over same scope
            if (initializer != null)
            {
                InitializeScope(key, valueFactory);
            }

            return scope.TryCreateLifetime(out lifetime);
        }

        private void InitializeScope<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IValueFactory<K, Scoped<V>>
        {
            var init = initializer;

            if (init != null)
            {
                scope = init.CreateScope(key, valueFactory);
                Volatile.Write(ref initializer, null); // volatile write must occur after setting value
            }
        }

        /// <summary>
        /// Terminates the scope and disposes the value. Once the scope is terminated, it is no longer
        /// possible to create new lifetimes for the value.
        /// </summary>
        public void Dispose()
        {
            var init = initializer;

            if (init != null)
            {
                scope = init.TryCreateDisposedScope();
            }

            scope.Dispose();
        }

        private class Initializer
        {
            private bool isInitialized;
            private Scoped<V> value;

            public Scoped<V> CreateScope<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IValueFactory<K, Scoped<V>>
            {
                lock (this)
                {
                    if (isInitialized)
                    {
                        return value;
                    }

                    value = valueFactory.Create(key);
                    isInitialized = true;

                    return value;
                }
            }

            public Scoped<V> TryCreateDisposedScope()
            {
                lock (this)
                {
                    if (isInitialized)
                    {
                        return value;
                    }

                    // don't expose to other threads until disposed (else they may use the invalid default value)
                    var temp = new Scoped<V>(default);
                    temp.Dispose();
                    value = temp;
                    isInitialized = true;

                    return value;
                }
            }
        }
    }
}
