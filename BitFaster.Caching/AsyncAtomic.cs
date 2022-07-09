using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class AsyncAtomic<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public AsyncAtomic()
        {
            this.initializer = new Initializer();
        }

        public AsyncAtomic(V value)
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
            private Task<V> valueTask;

            public V CreateValue(K key, Func<K, V> valueFactory)
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = Synchronized.Initialize(ref this.valueTask, ref isInitialized, ref syncLock, tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = valueFactory(key);
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

                // TODO: how dangerous is this?
                // it can block forever if value factory blocks
                return synchronizedTask.GetAwaiter().GetResult();
            }

            public async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = Synchronized.Initialize(ref this.valueTask, ref isInitialized, ref syncLock, tcs.Task);

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
        }
    }

    internal static class Synchronized
    {
        public static V Initialize<V>(ref V target, ref bool initialized, ref object syncLock, V value)
        {
            // Fast path
            if (Volatile.Read(ref initialized))
            {
                return target;
            }

            lock (syncLock)
            {
                if (!Volatile.Read(ref initialized))
                {
                    target = value;
                    Volatile.Write(ref initialized, true);
                }
            }

            return target;
        }
    }
}
