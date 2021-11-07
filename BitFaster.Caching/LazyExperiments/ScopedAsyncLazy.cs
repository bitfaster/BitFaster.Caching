using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.LazyExperiments
{
    // Enable caching an AsyncLazy disposable object - guarantee single instance, safe disposal
    public class ScopedAsyncLazy<TValue> : IDisposable 
        where TValue : IDisposable
    {
        private ReferenceCount<AsyncLazy<TValue>> refCount;
        private bool isDisposed;

        private readonly Func<Task<TValue>> valueFactory;

        private readonly AsyncLazy<TValue> lazy;

        // should this even be allowed?
        public ScopedAsyncLazy(Func<TValue> valueFactory)
        {
            this.lazy = new AsyncLazy<TValue>(() => Task.FromResult(valueFactory()));
        }

        public ScopedAsyncLazy(Func<Task<TValue>> valueFactory)
        {
            this.lazy = new AsyncLazy<TValue>(valueFactory);
        }

        public async Task<Lifetime<AsyncLazy<TValue>>> CreateLifetimeAsync()
        {
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
                    return new Lifetime<AsyncLazy<TValue>>(newRefCount, this.DecrementReferenceCount);
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
                            // TODO: badness
                            newRefCount.Value.Task.GetAwaiter().GetResult().Dispose();
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
