using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Lightweight.Caching
{
    /// <summary>
    /// A wrapper for IDisposable objects stored in a cache. If the object is used in a long
    /// running operation and disposed by the cache, the scope can create a lifetime that
    /// prevents the wrapped object from being diposed until the calling code completes.
    /// </summary>
    /// <typeparam name="T">The type of scoped value.</typeparam>
    public class Scoped<T> : IDisposable where T : IDisposable
    {
        private ReferenceCount<T> refCount;
        private bool isDisposed;

        public Scoped(T value)
        {
            this.refCount = new ReferenceCount<T>(value);
        }

        public Lifetime CreateLifetime()
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
                    return new Lifetime(oldRefCount.Value, this.DecrementReferenceCount);
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

        public class Lifetime : IDisposable
        {
            private readonly Action onDisposeAction;
            private bool isDisposed;

            public Lifetime(T value, Action onDisposeAction)
            {
                this.Value = value;
                this.onDisposeAction = onDisposeAction;
            }

            public T Value { get; }

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
}
