using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    // TODO: is this actually even needed? Or is the approach in ScopedAsyncAtomic sufficient? E.g. rely on IsValueCreated at dispose time, scoped owns tracking dispose and is already thread safe.
    // requirements for IDisposable atomic
    // if value !created, no dispose, cannot create - throws object disposed exception
    // if created, dispose value
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class DisposableAtomic<K, V> : IDisposable where V : IDisposable
    {
        private volatile Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public DisposableAtomic()
        {
            this.initializer = new Initializer();
        }

        public DisposableAtomic(V value)
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

        public void Dispose()
        {
            Initializer init = this.initializer;

            if (init != null)
            {
                init.Dispose();
            }
            else
            {
                this.value?.Dispose();
            }
        }

        private class Initializer : IDisposable
        {
            private object syncLock = new object();
            private bool isInitialized;
            private volatile bool isDisposed;
            private V value;

            public V CreateValue(K key, Func<K, V> valueFactory)
            {
                var r = Synchronized.Initialize(ref this.value, ref isInitialized, ref syncLock, valueFactory, key);

                // 2 possible orders
                // Create value then Dispose
                // Dispose then CreateValue

                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(nameof(value));
                }

                return r;
            }

            public void Dispose()
            {
                lock (this.syncLock)
                {
                    if (this.isInitialized)
                    {
                        value.Dispose();
                    }

                    // LazyInitializer will no longer attempt to init in CreateValue
                    Volatile.Write(ref this.isInitialized, true);
                }

                this.isDisposed = true;
            }
        }
    }
}
