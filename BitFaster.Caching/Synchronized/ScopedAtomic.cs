using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    // Requirements:
    // 1. Exactly once disposal.
    // 2. Exactly once invocation of value factory (synchronized create).
    // 3. Resolve race between create dispose init, if disposed is called before value is created, scoped value is disposed for life.
    public class ScopedAtomic<K, V> : IScoped<V>, IDisposable where V : IDisposable
    {
        private Scoped<V> scope;
        private Initializer initializer;

        public ScopedAtomic()
        {
            initializer = new Initializer();
        }

        public ScopedAtomic(V value)
        {
            scope = new Scoped<V>(value);
        }

        public bool TryCreateLifetime(K key, Func<K, V> valueFactory, out Lifetime<V> lifetime)
        {
            if(scope?.IsDisposed ?? false)
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

        private void InitializeScope(K key, Func<K, V> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                scope = init.CreateScope(key, valueFactory);
                initializer = null;
            }
        }
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
            private object syncLock = new object();
            private bool isInitialized;
            private Scoped<V> value;

            public Scoped<V> CreateScope(K key, Func<K, V> valueFactory)
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

                    value = new Scoped<V>(valueFactory(key));
                    Volatile.Write(ref isInitialized, true);

                    return value;
                }
            }

            public Scoped<V> TryCreateDisposedScope()
            {
                // already exists, return it
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

                    // don't expose to other threads until disposed
                    var temp = new Scoped<V>(default);
                    temp.Dispose();
                    value = temp;
                    Volatile.Write(ref isInitialized, true);

                    return value;
                }
            }
        }
    }
}
