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

    // TODO: how close is Handle to a scoped instance?
    
    public class ScopedAtom<K, V> : IScoped<V>, IDisposable where V : IDisposable
    {
        private Handle handle;
        private Initializer initializer;

        public ScopedAtom()
        {
            initializer = new Initializer();
        }

        public ScopedAtom(V value)
        {
            handle = new Handle() { refCount = new ReferenceCount<V>(value) };
        }

        public bool TryCreateLifetime(K key, Func<K, V> valueFactory, out Lifetime<V> lifetime)
        {
            // if disposed, return
            if (handle?.refCount.Count == 0)
            {
                lifetime = default;
                return false;
            }

            // Create handle EXACTLY once, ref count cas operates over same handle
            if (initializer != null)
            {
                InitializeHandle(key, valueFactory);
            }

            return handle.TryCreateLifetime(out lifetime);
        }

        private void InitializeHandle(K key, Func<K, V> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                handle = init.CreateHandle(key, valueFactory);
                initializer = null;
            }
        }
        public void Dispose()
        {
            var init = initializer;

            if (init != null)
            {
                handle = init.TryCreateDisposedHandle();
            }

            handle.DecrementReferenceCount();
        }

        private class Handle
        {
            public ReferenceCount<V> refCount;

            public bool TryCreateLifetime(out Lifetime<V> lifetime)
            {
                while (true)
                {
                    var oldRefCount = refCount;

                    // If old ref count is 0, the scoped object has been disposed.
                    if (oldRefCount.Count == 0)
                    {
                        lifetime = default;
                        return false;
                    }

                    if (oldRefCount == Interlocked.CompareExchange(ref refCount, oldRefCount.IncrementCopy(), oldRefCount))
                    {
                        // When Lifetime is disposed, it calls DecrementReferenceCount
                        lifetime = new Lifetime<V>(oldRefCount, DecrementReferenceCount);
                        return true;
                    }
                }
            }

            public void DecrementReferenceCount()
            {
                while (true)
                {
                    var oldRefCount = refCount;

                    if (oldRefCount.Count == 0)
                    {
                        return;
                    }

                    if (oldRefCount == Interlocked.CompareExchange(ref refCount, oldRefCount.DecrementCopy(), oldRefCount))
                    {
                        if (refCount.Count == 0)
                        {
                            refCount.Value.Dispose();
                        }

                        break;
                    }
                }
            }
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isInitialized;
            private Handle value;

            public Handle CreateHandle(K key, Func<K, V> valueFactory)
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

                    value = new Handle { refCount = new ReferenceCount<V>(valueFactory(key)) };
                    Volatile.Write(ref isInitialized, true);

                    return value;
                }
            }

            public Handle TryCreateDisposedHandle()
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

                    // zero == Disposed, start at zero via decrememt copy
                    value = new Handle() { refCount = new ReferenceCount<V>(default).DecrementCopy() };
                    Volatile.Write(ref isInitialized, true);

                    return value;
                }
            }
        }
    }
}
