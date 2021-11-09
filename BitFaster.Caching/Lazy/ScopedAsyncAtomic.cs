using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    // Enable caching an AsyncLazy disposable object - guarantee single instance, safe disposal
    public class ScopedAsyncAtomic<K, TValue> : IDisposable 
        where TValue : IDisposable
    {
        private ReferenceCount<AsyncAtomic<K, TValue>> refCount;
        private bool isDisposed;

        private readonly AsyncAtomic<K, TValue> asyncAtomic;

        public ScopedAsyncAtomic()
        {
            this.asyncAtomic = new AsyncAtomic<K, TValue>();
        }

        public async Task<AsyncAtomicLifetime<K, TValue>> CreateLifetimeAsync(K key, Func<K, Task<TValue>> valueFactory)
        {
            // TODO: inside the loop?
            if (this.isDisposed)
            {
                throw new ObjectDisposedException($"{nameof(TValue)} is disposed.");
            }

            await this.asyncAtomic.GetValueAsync(key, valueFactory).ConfigureAwait(false);

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
                    return new AsyncAtomicLifetime<K, TValue>(newRefCount, this.DecrementReferenceCount);
                }
            }
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
}
