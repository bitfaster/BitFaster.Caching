using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    // Enable caching an AsyncLazy disposable object - guarantee single instance, safe disposal
#if NETCOREAPP3_1_OR_GREATER
    public class ScopedAsyncLazy<TValue> : IAsyncDisposable 
        where TValue : IDisposable
    {
        private ReferenceCount<AtomicAsyncLazy<TValue>> refCount;
        private bool isDisposed;

        private readonly AtomicAsyncLazy<TValue> lazy;

        // should this even be allowed?
        public ScopedAsyncLazy(Func<TValue> valueFactory)
        {
            this.lazy = new AtomicAsyncLazy<TValue>(() => Task.FromResult(valueFactory()));
        }

        public ScopedAsyncLazy(Func<Task<TValue>> valueFactory)
        {
            this.lazy = new AtomicAsyncLazy<TValue>(valueFactory);
        }

        public async Task<AsyncLazyLifetime<TValue>> CreateLifetimeAsync()
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
                    var value = await this.lazy;
                    return new AsyncLazyLifetime<TValue>(newRefCount, this.DecrementReferenceCountAsync);
                }
            }
        }

        // TODO: Do we need an async lifetime?
        private async Task DecrementReferenceCountAsync()
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
                            var v = await newRefCount.Value;
                            v.Dispose();
                        }
                    }

                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!this.isDisposed)
            {
                await this.DecrementReferenceCountAsync();
                this.isDisposed = true;
            }
        }
    }
#endif
}
