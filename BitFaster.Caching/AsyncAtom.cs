﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class AsyncAtom<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public AsyncAtom()
        {
            this.initializer = new Initializer();
        }

        public AsyncAtom(V value)
        {
            this.value = value;
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
                    return this.valueTask;
                }

                lock (syncLock)
                {
                    if (!Volatile.Read(ref isInitialized))
                    {
                        this.valueTask = value;
                        Volatile.Write(ref isInitialized, true);
                    }
                }

                return this.valueTask;
            }
        }
    }
}