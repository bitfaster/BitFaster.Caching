using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    // Enable caching an AsyncLazy disposable object - guarantee single instance, safe disposal
#if NETCOREAPP3_1_OR_GREATER
    public class ScopedAtomicAsync<K, TValue> : IDisposable 
        where TValue : IDisposable
    {
        private ReferenceCount<AsyncAtomic<K, TValue>> refCount;
        private bool isDisposed;

        private readonly AsyncAtomic<K, TValue> lazy;

        // should this even be allowed?
        public ScopedAtomicAsync()
        {
            this.lazy = new AsyncAtomic<K, TValue>();
        }

        //public ScopedAtomicAsync(Func<Task<TValue>> valueFactory)
        //{
        //    this.lazy = new AsyncAtomic<K, TValue>(valueFactory);
        //}

        public async Task<AsyncAtomicLifetime<K, TValue>> CreateLifetimeAsync()
        {
            // TODO: inside the loop?
            if (this.isDisposed)
            {
                throw new ObjectDisposedException($"{nameof(TValue)} is disposed.");
            }

            while (true)
            {
                // IncrementCopy will throw ObjectDisposedException if the referenced object has no references.
                // This mitigates the race where the value is disposed after the above check is run.
                var oldRefCount = this.refCount;
                var newRefCount = oldRefCount.IncrementCopy();

                // guarantee ref held before lazy evaluated
                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
                {
                    // When Lease is disposed, it calls DecrementReferenceCount
                    //var value = await this.lazy;
                    return new AsyncAtomicLifetime<K, TValue>(newRefCount, this.DecrementReferenceCount);
                }
            }
        }

        // TODO: Do we need an async lifetime?
        private void DecrementReferenceCount()
        {
            while (true)
            {
                var oldRefCount = this.refCount;
                var newRefCount = oldRefCount.DecrementCopy();

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
                {
                    // TODO: how to prevent a race here? Need to use the lock inside the lazy?
                    // Do we need atomic disposable?
                    if (newRefCount.Count == 0)
                    {
                        if (newRefCount.Value.IsValueCreated)
                        {
                            newRefCount.Value.ValueIfCreated?.Dispose();
                        }
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
#endif
}
