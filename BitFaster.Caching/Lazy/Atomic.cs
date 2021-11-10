using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // https://github.com/dotnet/runtime/issues/27421
    // https://github.com/alastairtree/LazyCache/issues/73
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class Atomic<K, V>
    {
        private volatile Initializer initializer;

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

            return CreateValue(valueFactory, key);
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

        private V CreateValue(Func<K, V> valueFactory, K key)
        {
            Initializer init = this.initializer;

            if (init != null)
            {
                this.value = init.CreateValue(valueFactory, key);
                this.initializer = null;
            }

            return this.value;
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isInitialized;
            private V value;

            public V CreateValue(Func<K, V> valueFactory, K key)
            {
                return Synchronized.Initialize(ref this.value, ref isInitialized, ref syncLock, valueFactory, key);
            }
        }
    }
}
