using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A class that provides simple, lightweight exactly once initialization for scoped values
    /// stored in a cache.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    [DebuggerDisplay("IsScopeCreated={initializer == null}, Value={ScopeIfCreated}")]
    public sealed class ScopedAsyncAtomicFactory<K, V> : IScoped<V>, IDisposable
        where K : notnull
        where V : IDisposable
    {
        private Scoped<V>? scope;
        private Initializer? initializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopedAsyncAtomicFactory{K, V}"/> class.
        /// </summary>
        public ScopedAsyncAtomicFactory()
        {
            initializer = new Initializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopedAsyncAtomicFactory{K, V}"/> class with the
        /// specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public ScopedAsyncAtomicFactory(V value)
        {
            scope = new Scoped<V>(value);
        }

        /// <summary>
        /// Gets a value indicating whether the scope has been initialized.
        /// </summary>
        public bool IsScopeCreated => initializer == null;

        /// <summary>
        /// Gets the scope if it has been initialized, else default.
        /// </summary>
        public Scoped<V>? ScopeIfCreated
        {
            get
            {
                if (initializer != null)
                {
                    return default;
                }

                return scope;
            }
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="lifetime">When this method returns, contains the Lifetime that was created, or the default value of the type if the operation failed.</param>
        /// <returns>true if the Lifetime was created; otherwise false.</returns>
        public bool TryCreateLifetime([MaybeNullWhen(false)] out Lifetime<V> lifetime)
        {
            if (scope?.IsDisposed ?? false || initializer != null)
            {
                lifetime = default;
                return false;
            }

            return scope!.TryCreateLifetime(out lifetime);
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <param name="key">The key associated with the scoped value.</param>
        /// <param name="valueFactory">The value factory to use to create the scoped value when it is not initialized.</param>
        /// <returns>true if the Lifetime was created; otherwise false. If the lifetime was created, the new lifetime is also returned.</returns>
        // backcompat: remove
        public ValueTask<(bool success, Lifetime<V>? lifetime)> TryCreateLifetimeAsync(K key, Func<K, Task<Scoped<V>>> valueFactory)
        {
            return TryCreateLifetimeAsync(key, new AsyncValueFactory<K, Scoped<V>>(valueFactory));
        }

        /// <summary>
        /// Attempts to create a lifetime for the scoped value. The lifetime guarantees the value is alive until 
        /// the lifetime is disposed.
        /// </summary>
        /// <typeparam name="TFactory">The type of the value factory.</typeparam>
        /// <param name="key">The key associated with the scoped value.</param>
        /// <param name="valueFactory">The value factory to use to create the scoped value when it is not initialized.</param>
        /// <returns>true if the Lifetime was created; otherwise false. If the lifetime was created, the new lifetime is also returned.</returns>
        public async ValueTask<(bool success, Lifetime<V>? lifetime)> TryCreateLifetimeAsync<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IAsyncValueFactory<K, Scoped<V>>
        {
            // if disposed, return
            if (scope?.IsDisposed ?? false)
            {
                return (false, default);
            }

            // Create scope EXACTLY once, ref count cas operates over same scope
            if (initializer != null)
            {
                await InitializeScopeAsync(key, valueFactory).ConfigureAwait(false);
            }

            bool res = scope!.TryCreateLifetime(out var lifetime);
            return (res, lifetime);
        }

        private async ValueTask InitializeScopeAsync<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IAsyncValueFactory<K, Scoped<V>>
        {
            var init = Volatile.Read(ref initializer);

            if (init != null)
            {
                scope = await init.CreateScopeAsync(key, valueFactory).ConfigureAwait(false);
                Volatile.Write(ref initializer, null);
            }
        }

        /// <summary>
        /// Terminates the scope and disposes the value. Once the scope is terminated, it is no longer
        /// possible to create new lifetimes for the value.
        /// </summary>
        public void Dispose()
        {
            var init = initializer;

            if (init != null && init.TryGetScope(out var disposeScope))
            {
                scope = disposeScope;
            }

            // It is possible that a task was running to create the scope, but it didn't complete yet
            // in that case this.scope == null. Initializer is now marked for dispose, and the new 
            // scope will be disposed when the task completes.
            scope?.Dispose();
        }

        private class Initializer
        {
            private bool isTaskInitialized;
            private bool isTaskCompleted;
            private bool isDisposeRequested;
            private Task<Scoped<V>>? task;

            public async ValueTask<Scoped<V>> CreateScopeAsync<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IAsyncValueFactory<K, Scoped<V>>
            {
                var tcs = new TaskCompletionSource<Scoped<V>>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = DoubleCheck(tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var scope = await valueFactory.CreateAsync(key).ConfigureAwait(false);
                        tcs.SetResult(scope);

                        Volatile.Write(ref isTaskCompleted, true);

                        if (Volatile.Read(ref isDisposeRequested))
                        {
                            scope.Dispose();
                        }

                        return scope;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref isTaskInitialized, false);
                        tcs.SetException(ex);

                        // always await the task to avoid unobserved task exceptions - normal case is that no other task is waiting.
                        // this will re-throw the exception.
                        await tcs.Task.ConfigureAwait(false);
                    }
                }

                return await synchronizedTask.ConfigureAwait(false);
            }

#pragma warning disable CA2002 // Do not lock on objects with weak identity
            private Task<Scoped<V>> DoubleCheck(Task<Scoped<V>> value)
            {
                // Fast path
                if (Volatile.Read(ref isTaskInitialized))
                {
                    return task!;
                }

                lock (this)
                {
                    if (!isTaskInitialized)
                    {
                        task = value;
                        isTaskInitialized = true;
                    }
                }

                return task!;
            }
#pragma warning restore CA2002 // Do not lock on objects with weak identity

            // <remarks>
            // Let's say there are 2 threads, A and B:
            // A is the init thread
            //    1. mark isTaskInitialized = true
            //    2. read is dispose requested
            // B is the dispose thread
            //    1. mark dispose requested = true
            //    2. read isTaskInitialized
            // Due to the rules of volatile, these reads and writes cannot be reordered. 
            // Therefore, the dispose race reduces to two possible scenarios:
            // 1. If init task is completed, we can return it, then dispose it
            // 2. If it is not yet completed, it is guaranteed to dispose on completion because volatile writes cannot be re-ordered.
            // If the value factory continuously throws, the object will be neither created nor disposed. This is considered benign.
            // </remarks>
            public bool TryGetScope([MaybeNullWhen(false)] out Scoped<V> scope)
            {
                Volatile.Write(ref this.isDisposeRequested, true);

                if (Volatile.Read(ref isTaskCompleted))
                {
                    // isTaskCompleted is only set when there is no exception, so this is safe to return
                    scope = task!.Result;
                    return true;
                }

                scope = default;
                return false;
            }
        }
    }
}
