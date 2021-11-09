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

        public bool TryCreateLifetime(K key, Func<K, V> valueFactory, out AtomicLifetime<K, V> lifetime)
        {
            // TODO: inside the loop?
            if (this.isDisposed)
            {
                lifetime = default(AtomicLifetime<K, V>);
                return false;
            }

            // initialize - factory can throw so do this before we start counting refs
            this.refCount.Value.GetValue(key, valueFactory);

            while (true)
            {
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

    public class AtomicLifetime<K, V> : IDisposable where V : IDisposable
    {
        private readonly Action onDisposeAction;
        private readonly ReferenceCount<DisposableAtomic<K, V>> refCount;
        private bool isDisposed;

        public AtomicLifetime(ReferenceCount<DisposableAtomic<K, V>> refCount, Action onDisposeAction)
        {
            this.refCount = refCount;
            this.onDisposeAction = onDisposeAction;
        }

        public V Value => this.refCount.Value.ValueIfCreated;

        public int ReferenceCount => this.refCount.Count;

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.onDisposeAction();
                this.isDisposed = true;
            }
        }
    }
}
