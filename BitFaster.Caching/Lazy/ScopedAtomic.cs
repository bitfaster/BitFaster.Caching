using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    public class ScopedAtomic<K, V> : IDisposable where V : IDisposable
    {
        private ReferenceCount<DisposableAtomic<K, V>> refCount;
        private bool isDisposed;

        public ScopedAtomic()
        {
            this.refCount = new ReferenceCount<DisposableAtomic<K, V>>(new DisposableAtomic<K, V>());
        }

        public ScopedAtomic(V value)
        {
            this.refCount = new ReferenceCount<DisposableAtomic<K, V>>(new DisposableAtomic<K, V>(value));
        }

        public bool TryCreateLifetime(K key, Func<K, V> valueFactory, out AtomicLifetime<K, V> lifetime)
        {
            // initialize - factory can throw so do this before we start counting refs
            this.refCount.Value.GetValue(key, valueFactory);

            // TODO: exact dupe
            while (true)
            {
                var oldRefCount = this.refCount;
               
                // If old ref count is 0, the scoped object has been disposed and there was a race.
                if (this.isDisposed || oldRefCount.Count == 0)
                { 
                    lifetime = default;
                    return false;
                }

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, oldRefCount.IncrementCopy(), oldRefCount))
                {
                    // When Lifetime is disposed, it calls DecrementReferenceCount
                    lifetime = new AtomicLifetime<K, V>(oldRefCount, this.DecrementReferenceCount);
                    return true;
                }
            }
        }

        public bool TryCreateLifetime(out AtomicLifetime<K, V> lifetime)
        {
            if (!this.refCount.Value.IsValueCreated)
            {
                lifetime = default;
                return false;
            }

            // TODO: exact dupe
            while (true)
            {
                var oldRefCount = this.refCount;

                // If old ref count is 0, the scoped object has been disposed and there was a race.
                if (this.isDisposed || oldRefCount.Count == 0)
                {
                    lifetime = default;
                    return false;
                }

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, oldRefCount.IncrementCopy(), oldRefCount))
                {
                    // When Lifetime is disposed, it calls DecrementReferenceCount
                    lifetime = new AtomicLifetime<K, V>(oldRefCount, this.DecrementReferenceCount);
                    return true;
                }
            }
        }

        public AtomicLifetime<K, V> CreateLifetime(K key, Func<K, V> valueFactory)
        {
            if (!TryCreateLifetime(key, valueFactory, out var lifetime))
            {
                throw new ObjectDisposedException($"{nameof(V)} is disposed.");
            }

            return lifetime;
        }

        private void DecrementReferenceCount()
        {
            while (true)
            {
                var oldRefCount = this.refCount;

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, oldRefCount.DecrementCopy(), oldRefCount))
                {
                    if (this.refCount.Count == 0)
                    {
                        this.refCount.Value.Dispose();
                    }

                    break;
                }
            }
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.DecrementReferenceCount();
                this.isDisposed = true;
            }
        }
    }
}
