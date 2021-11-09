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
            // TODO: inside the loop?
            if (this.isDisposed)
            {
                lifetime = default;
                return false;
            }

            // initialize - factory can throw so do this before we start counting refs
            this.refCount.Value.GetValue(key, valueFactory);

            while (true)
            {
                // TODO: this increment copy logic was removed - verify how this is intended to work.
                // Could we simply check the value of IncrementCopy == 1 (meaning it started at zero and was therefore disposed?)
                // IncrementCopy will throw ObjectDisposedException if the referenced object has no references.
                // This mitigates the race where the value is disposed after the above check is run.
                var oldRefCount = this.refCount;
                var newRefCount = oldRefCount.IncrementCopy();
                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
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
                var newRefCount = oldRefCount.DecrementCopy();

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
                {
                    if (newRefCount.Count == 0)
                    {
                        newRefCount.Value.Dispose();
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
