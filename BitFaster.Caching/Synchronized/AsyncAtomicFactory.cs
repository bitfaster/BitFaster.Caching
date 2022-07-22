using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class AsyncAtomicFactory<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public AsyncAtomicFactory()
        {
            initializer = new Initializer();
        }

        public AsyncAtomicFactory(V value)
        {
            this.value = value;
        }

        public async Task<V> GetValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            if (initializer == null)
            {
                return value;
            }

            return await CreateValueAsync(key, valueFactory).ConfigureAwait(false);
        }

        public bool IsValueCreated => initializer == null;

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

        private async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                value = await init.CreateValueAsync(key, valueFactory).ConfigureAwait(false);
                initializer = null;
            }

            return value;
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isInitialized;
            private Task<V> valueTask;

            public async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
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
