using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BitFaster.Caching
{
    /// <summary>
    /// A lifetime scope for IDisposable objects stored in a cache. If the object is used in a long
    /// running operation and disposed by a cache, the scope can create a lifetime that prevents
    /// the wrapped object from being diposed until the calling code completes.
    /// </summary>
    /// <typeparam name="T">The type of scoped value.</typeparam>
    public class Scoped<T> : IDisposable where T : IDisposable
    {
        private ReferenceCount<T> refCount;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new Scoped value.
        /// </summary>
        /// <param name="value">The value to scope.</param>
        public Scoped(T value)
        {
            this.refCount = new ReferenceCount<T>(value);
        }

        /// <summary>
        /// Creates a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <returns>A value lifetime.</returns>
        /// <exception cref="ObjectDisposedException">The scope is disposed.</exception>
        public Lifetime<T> CreateLifetime()
        {
            if (!TryCreateLifetime(out var lifetime))
            {
                throw new ObjectDisposedException($"{nameof(T)} is disposed.");
            }

            return lifetime;
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        public bool TryCreateLifetime(out Lifetime<T> lifetime)
        {
            if (this.isDisposed)
            {
                lifetime = default(Lifetime<T>);
                return false;
            }

            while (true)
            {
                // IncrementCopy will throw ObjectDisposedException if the referenced object has no references.
                // This mitigates the race where the value is disposed after the above check is run.
                var oldRefCount = this.refCount;
                var newRefCount = oldRefCount.IncrementCopy();

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
                {
                    // When Lifetime is disposed, it calls DecrementReferenceCount
                    lifetime = new Lifetime<T>(oldRefCount, this.DecrementReferenceCount);
                    return true;
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

        /// <summary>
        /// Terminates the scope and disposes the value. Once the scope is terminated, it is no longer
        /// possible to create new lifetimes for the value.
        /// </summary>
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
