﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BitFaster.Caching
{
    // Enable caching a Lazy disposable object - guarantee single instance, safe disposal
    public class ScopedLazy<T> : IDisposable
        where T : IDisposable
    {
        private ReferenceCount<Lazy<T>> refCount;
        private bool isDisposed;

        public ScopedLazy(Func<T> valueFactory)
        {
            // TODO: this will cache exceptions
            var lazy = new Lazy<T>(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
            this.refCount = new ReferenceCount<Lazy<T>>(lazy);
        }

        public Lifetime<T> CreateLifetime()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException($"{nameof(T)} is disposed.");
            }

            while (true)
            {
                // IncrementCopy will throw ObjectDisposedException if the referenced object has no references.
                // This mitigates the race where the value is disposed after the above check is run.
                var oldRefCount = this.refCount;
                var newRefCount = oldRefCount.IncrementCopy();

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
                {
                    // When Lease is disposed, it calls DecrementReferenceCount
                    return new Lifetime<T>(oldRefCount.Value.Value, this.DecrementReferenceCount);
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
                            newRefCount.Value.Value.Dispose();
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
