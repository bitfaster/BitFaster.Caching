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
    public sealed class Scoped<T> : IScoped<T>, IDisposable, ILifetimeReleaser where T : IDisposable
    {
        private const int DisposedFlag = unchecked((int)0x80000000);
        private const int ReferenceCountMask = int.MaxValue;

        private readonly T value;
        private int state = 1;

        /// <summary>
        /// Initializes a new Scoped value.
        /// </summary>
        /// <param name="value">The value to scope.</param>
        public Scoped(T value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the scope is disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref this.state) < 0;

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        public bool TryCreateLifetime([MaybeNullWhen(false)] out Lifetime<T> lifetime)
        {
            while (true)
            {
                int oldState = Volatile.Read(ref this.state);

                if (oldState < 0)
                {
                    lifetime = default;
                    return false;
                }

                if (Interlocked.CompareExchange(ref this.state, oldState + 1, oldState) == oldState)
                {
                    lifetime = new Lifetime<T>(this.value, oldState & ReferenceCountMask, this);
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
                Throw.Disposed<T>();

            return lifetime;
        }

        void ILifetimeReleaser.ReleaseLifetime()
        {
            while (true)
            {
                int oldState = Volatile.Read(ref this.state);
                int oldReferenceCount = oldState & ReferenceCountMask;
                int newReferenceCount = oldReferenceCount - 1;
                int newState = (oldState & DisposedFlag) | newReferenceCount;

                if (Interlocked.CompareExchange(ref this.state, newState, oldState) == oldState)
                {
                    if (newReferenceCount == 0 && (oldState & DisposedFlag) != 0)
                    {
                        this.value?.Dispose();
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Terminates the scope and disposes the value. Once the scope is terminated, it is no longer
        /// possible to create new lifetimes for the value.
        /// </summary>
        public void Dispose()
        {
            while (true)
            {
                int oldState = Volatile.Read(ref this.state);

                if ((oldState & DisposedFlag) != 0)
                {
                    return;
                }

                int oldReferenceCount = oldState & ReferenceCountMask;
                int newReferenceCount = oldReferenceCount - 1;
                int newState = DisposedFlag | newReferenceCount;

                if (Interlocked.CompareExchange(ref this.state, newState, oldState) == oldState)
                {
                    if (newReferenceCount == 0)
                    {
                        this.value?.Dispose();
                    }

                    return;
                }
            }
        }

        [ExcludeFromCodeCoverage]
        internal string FormatDebug()
        {
            if (IsDisposed)
            {
                return "[Disposed Scope]";
            }

            return this.value?.ToString() ?? "[null]";
        }

        [ExcludeFromCodeCoverage]
        internal class ScopedDebugView
        {
            private readonly Scoped<T> scoped;

            public ScopedDebugView(Scoped<T> scoped)
            {
                if (scoped is null)
                    Throw.ArgNull(ExceptionArgument.scoped);

                this.scoped = scoped;
            }

            public bool IsDisposed => this.scoped.IsDisposed;

            public T Value => this.scoped.value;
        }
    }
}
