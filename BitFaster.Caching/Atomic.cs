using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // make a version of Atomic that is baised towards sync usage.
    // Caller can then choose between async or async optimized version that still works with both.
    // SHould benchmark whether the AsyncAtomic version is meaninfully worse in terms of latency/allocs
    // Looks like it would be very similar except the additional TaskCompletionSource alloc
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class Atomic<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public Atomic()
        {
            this.initializer = new Initializer();
        }

        public Atomic(V value)
        {
            this.value = value;
        }

        public V GetValue(K key, Func<K, V> valueFactory)
        {
            if (this.initializer == null)
            {
                return this.value;
            }

            return CreateValue(key, valueFactory);
        }

        public async Task<V> GetValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            if (this.initializer == null)
            {
                return this.value;
            }

            return await CreateValueAsync(key, valueFactory).ConfigureAwait(false);
        }

        public bool IsValueCreated => this.initializer == null;

        public V ValueIfCreated
        {
            get
            {
                if (!this.IsValueCreated)
                {
                    return default;
                }

                return this.value;
            }
        }

        private V CreateValue(K key, Func<K, V> valueFactory)
        {
            Initializer init = this.initializer;

            if (init != null)
            {
                this.value = init.CreateValue(key, valueFactory);
                this.initializer = null;
            }

            return this.value;
        }

        private async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            Initializer init = this.initializer;

            if (init != null)
            {
                this.value = await init.CreateValueAsync(key, valueFactory).ConfigureAwait(false);
                this.initializer = null;
            }

            return this.value;
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isInitialized;
            private V value;

            public V CreateValue(K key, Func<K, V> valueFactory)
            {
                if (!Volatile.Read(ref isInitialized))
                {
                    return value;
                }

                lock (syncLock)
                {
                    if (!Volatile.Read(ref isInitialized))
                    {
                        return value;
                    }

                    value = valueFactory(key);
                    Volatile.Write(ref isInitialized, true);
                    return value;
                }
            }

            // This is terrifyingly bad on many levels.
            public async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
            {
                if (!Volatile.Read(ref isInitialized))
                {
                    return value;
                }

                // start another thread that holds the lock until a signal is sent.
                ManualResetEvent manualResetEvent = new ManualResetEvent(false);

                var lockTask = Task.Run(() => {
                    lock (syncLock)
                    {
                        if (!Volatile.Read(ref isInitialized))
                        {
                            // EXIT somehow and return value
                        }

                        manualResetEvent.WaitOne();
                    }
                });

                // Problems:
                // 1. what if value factory throws? We need to release the lock
                // 2. how to do double checked lock in the other thread
                value = await valueFactory(key);
                Volatile.Write(ref isInitialized, true);
                manualResetEvent.Set();
                await lockTask;

                return value;
            }
        }
    }
}
