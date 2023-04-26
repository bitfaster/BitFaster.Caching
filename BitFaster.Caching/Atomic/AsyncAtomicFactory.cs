using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A class that provides simple, lightweight exactly once initialization for values
    /// stored in a cache.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public sealed class AsyncAtomicFactory<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAtomicFactory{K, V}"/> class.
        /// </summary>
        public AsyncAtomicFactory()
        {
            initializer = new Initializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAtomicFactory{K, V}"/> class with the
        /// specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public AsyncAtomicFactory(V value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the value. If <see cref="IsValueCreated"/> is false, calling <see cref="GetValueAsync"/> will force initialization via the <paramref name="valueFactory"/> parameter.
        /// </summary>
        /// <param name="key">The key associated with the value.</param>
        /// <param name="valueFactory">The value factory to use to create the value when it is not initialized.</param>
        /// <returns>The value.</returns>
        public async ValueTask<V> GetValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            if (initializer == null)
            {
                return value;
            }

            return await CreateValueAsync(key, valueFactory).ConfigureAwait(false);
        }

        public async ValueTask<V> GetValueAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            if (initializer == null)
            {
                return value;
            }

            return await CreateValueAsync(key, valueFactory, factoryArgument).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a value indicating whether the value has been initialized.
        /// </summary>
        public bool IsValueCreated => initializer == null;

        /// <summary>
        /// Gets the value if it has been initialized, else default.
        /// </summary>
        public V ValueIfCreated
        {
            get
            {
                if (!IsValueCreated)
                {
                    return default;
                }

                return value;
            }
        }

        private async ValueTask<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                value = await init.CreateValueAsync(key, valueFactory).ConfigureAwait(false);
                initializer = null;
            }

            return value;
        }

        private async ValueTask<V> CreateValueAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg arg)
        {
            var init = initializer;

            if (init != null)
            {
                value = await init.CreateValueAsync(key, valueFactory, arg).ConfigureAwait(false);
                initializer = null;
            }

            return value;
        }

        private class Initializer
        {
            private readonly object syncLock = new object();
            private bool isInitialized;
            private Task<V> valueTask;

            public async ValueTask<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = DoubleCheck(tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = await valueFactory(key).ConfigureAwait(false);
                        tcs.SetResult(value);

                        return value;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref isInitialized, false);
                        tcs.SetException(ex);
                        throw;
                    }
                }

                return await synchronizedTask.ConfigureAwait(false);
            }

            public async ValueTask<V> CreateValueAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg arg)
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = DoubleCheck(tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = await valueFactory(key, arg).ConfigureAwait(false);
                        tcs.SetResult(value);

                        return value;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref isInitialized, false);
                        tcs.SetException(ex);
                        throw;
                    }
                }

                return await synchronizedTask.ConfigureAwait(false);
            }

            // TODO: hard to see how to de-dupe the code without introducing a new delegate/closure alloc
            public async ValueTask<V> CreateValueAsync2<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg arg)
            {
                var (ownTcs, synchronizedTask, tcs) = TryCreateTcs();

                if (ownTcs)
                {
                    try
                    {
                        var value = await valueFactory(key, arg).ConfigureAwait(false);
                        tcs.SetResult(value);
                        return value;
                    }
                    catch (Exception ex)
                    {
                        Fault(tcs, ex);
                        throw;
                    }
                }

                return await synchronizedTask.ConfigureAwait(false);
            }

            private (bool, Task<V>, TaskCompletionSource<V>) TryCreateTcs()
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);
                var synchronizedTask = DoubleCheck(tcs.Task);

                return (ReferenceEquals(synchronizedTask, tcs.Task), synchronizedTask, tcs);
            }

            private void Fault(TaskCompletionSource<V> tcs, Exception ex)
            {
                Volatile.Write(ref isInitialized, false);
                tcs.SetException(ex);
            }

            private Task<V> DoubleCheck(Task<V> value)
            {
                // Fast path
                if (Volatile.Read(ref isInitialized))
                {
                    return valueTask;
                }

                lock (syncLock)
                {
                    if (!Volatile.Read(ref isInitialized))
                    {
                        valueTask = value;
                        Volatile.Write(ref isInitialized, true);
                    }
                }

                return valueTask;
            }
        }
    }
}
