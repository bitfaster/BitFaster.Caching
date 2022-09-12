using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace BitFaster.Caching
{
    /// <summary>
    /// A lifetime scope for IDisposable objects stored in a cache. If the object is used in a long
    /// running operation and disposed by a cache, the scope can create a lifetime that prevents
    /// the wrapped object from being diposed until the calling code completes.
    /// </summary>
    /// <typeparam name="T">The type of scoped value.</typeparam>
    [DebuggerTypeProxy(typeof(Scoped<>.ScopedDebugView))]
    [DebuggerDisplay("{FormatDebug(),nq}")]
    public sealed class Scoped<T> : IScoped<T>, IDisposable where T : IDisposable
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
        /// Gets a value indicating whether the scope is disposed.
        /// </summary>
        public bool IsDisposed => isDisposed;

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        public bool TryCreateLifetime(out Lifetime<T> lifetime)
        {
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
                    lifetime = new Lifetime<T>(oldRefCount, this.DecrementReferenceCount);
                    return true;
                }
            }
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
                Ex.ThrowDisposed(nameof(T));
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
                        this.refCount.Value?.Dispose();
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

        [ExcludeFromCodeCoverage]
        internal string FormatDebug()
        {
            if (IsDisposed)
            {
                return "[Disposed Scope]";
            }

            return this.refCount.Value?.ToString();
        }

        [ExcludeFromCodeCoverage]
        internal class ScopedDebugView
        {
            private readonly Scoped<T> scoped;

            public ScopedDebugView(Scoped<T> scoped)
            {
                if (scoped is null)
                {
                    Ex.ThrowArgNull(nameof(scoped));
                }

                this.scoped = scoped;
            }

            public bool IsDisposed => this.scoped.IsDisposed;

            public T Value => this.scoped.refCount.Value;
        }
    }
}
