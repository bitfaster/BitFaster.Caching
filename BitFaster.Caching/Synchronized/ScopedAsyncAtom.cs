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
        private Handle handle;
        private Initializer initializer;

        public ScopedAsyncAtom()
        {
            initializer = new Initializer();
        }

        public ScopedAsyncAtom(V value)
        {
            handle = new Handle() { refCount = new ReferenceCount<V>(value) };
        }

        public async Task<(bool, Lifetime<V> lifetime)> TryCreateLifetimeAsync(K key, Func<K, Task<V>> valueFactory)
        {
            // if disposed, return
            if (handle?.refCount.Count == 0)
            {
                return (false, default);
            }

            // Create handle EXACTLY once, ref count cas operates over same handle
            if (initializer != null)
            {
                await InitializeHandleAsync(key, valueFactory).ConfigureAwait(false);
            }

            bool res = handle.TryCreateLifetime(out var lifetime);
            return (res, lifetime);
        }

        private async Task InitializeHandleAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                handle = await init.CreateHandleAsync(key, valueFactory).ConfigureAwait(false);
                initializer = null;
            }
        }
        public void Dispose()
        {
            var init = initializer;

            if (init != null && init.TryGetHandle(out var disposeHandle))
            {
                handle = disposeHandle;
            }

            // It is possible that a task was running to create the handle, but it didn't complete yet
            // in that case this.handle == null. Initializer is now marked for dispose, and the new 
            // handle will be disposed when the task completes.
            handle?.DecrementReferenceCount();
        }

        private class Handle
        {
            public ReferenceCount<V> refCount;

            public bool TryCreateLifetime(out Lifetime<V> lifetime)
            {
                while (true)
                {
                    var oldRefCount = refCount;

                    // If old ref count is 0, the scoped object has been disposed.
                    if (oldRefCount.Count == 0)
                    {
                        lifetime = default;
                        return false;
                    }

                    if (oldRefCount == Interlocked.CompareExchange(ref refCount, oldRefCount.IncrementCopy(), oldRefCount))
                    {
                        // When Lifetime is disposed, it calls DecrementReferenceCount
                        lifetime = new Lifetime<V>(oldRefCount, DecrementReferenceCount);
                        return true;
                    }
                }
            }

            public void DecrementReferenceCount()
            {
                while (true)
                {
                    var oldRefCount = refCount;

                    if (oldRefCount.Count == 0)
                    {
                        return;
                    }

                    if (oldRefCount == Interlocked.CompareExchange(ref refCount, oldRefCount.DecrementCopy(), oldRefCount))
                    {
                        if (refCount.Count == 0)
                        {
                            refCount.Value.Dispose();
                        }

                        break;
                    }
                }
            }
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isTaskInitialized;
            private bool isTaskCompleted;
            private bool isDisposeRequested;
            private Task<Handle> task;

            public async Task<Handle> CreateHandleAsync(K key, Func<K, Task<V>> valueFactory)
            {
                var tcs = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = DoubleCheck(tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = await valueFactory(key).ConfigureAwait(false);
                        var handle = new Handle() { refCount = new ReferenceCount<V>(value) };
                        tcs.SetResult(handle);

                        Volatile.Write(ref isTaskCompleted, true);

                        if (Volatile.Read(ref isDisposeRequested))
                        {
                            // aka dispose
                            handle.DecrementReferenceCount();
                        }

                        return handle;
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

            private Task<Handle> DoubleCheck(Task<Handle> value)
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
            public bool TryGetHandle(out Handle handle)
            {
                Volatile.Write(ref this.isDisposeRequested, true);

                if (Volatile.Read(ref isTaskCompleted))
                {
                    // isTaskCompleted is only set when there is no exception, so this is safe to return
                    handle = task.Result;
                    return true;
                }

                handle = default;
                return false;
            }
        }
    }
}
