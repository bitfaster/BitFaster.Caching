using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    public class ScopedAsyncAtom<K, V> : IScoped<V>, IDisposable where V : IDisposable
    {
        private Scoped<V> scope;
        private Initializer initializer;

        public ScopedAsyncAtom()
        {
            initializer = new Initializer();
        }

        public ScopedAsyncAtom(V value)
        {
            scope = new Scoped<V>(value);
        }

        public async Task<(bool, Lifetime<V> lifetime)> TryCreateLifetimeAsync(K key, Func<K, Task<V>> valueFactory)
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

            bool res = scope.TryCreateLifetime(out var lifetime);
            return (res, lifetime);
        }

        private async Task InitializeScopeAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                scope = await init.CreateScopeAsync(key, valueFactory).ConfigureAwait(false);
                initializer = null;
            }
        }
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
            private object syncLock = new object();
            private bool isTaskInitialized;
            private bool isTaskCompleted;
            private bool isDisposeRequested;
            private Task<Scoped<V>> task;

            public async Task<Scoped<V>> CreateScopeAsync(K key, Func<K, Task<V>> valueFactory)
            {
                var tcs = new TaskCompletionSource<Scoped<V>>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = DoubleCheck(tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = await valueFactory(key).ConfigureAwait(false);
                        var scope = new Scoped<V>(value);
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
                        throw;
                    }
                }

                return await synchronizedTask.ConfigureAwait(false);
            }

            private Task<Scoped<V>> DoubleCheck(Task<Scoped<V>> value)
            {
                // Fast path
                if (Volatile.Read(ref isTaskInitialized))
                {
                    return task;
                }

                lock (syncLock)
                {
                    if (!Volatile.Read(ref isTaskInitialized))
                    {
                        task = value;
                        Volatile.Write(ref isTaskInitialized, true);
                    }
                }

                return task;
            }

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
            public bool TryGetScope(out Scoped<V> scope)
            {
                Volatile.Write(ref this.isDisposeRequested, true);

                if (Volatile.Read(ref isTaskCompleted))
                {
                    // isTaskCompleted is only set when there is no exception, so this is safe to return
                    scope = task.Result;
                    return true;
                }

                scope = default;
                return false;
            }
        }
    }
}
