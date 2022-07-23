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
    public sealed class AtomicFactory<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public AtomicFactory()
        {
            initializer = new Initializer();
        }

        public AtomicFactory(V value)
        {
            this.value = value;
        }

        public V GetValue(K key, Func<K, V> valueFactory)
        {
            if (initializer == null)
            {
                return value;
            }

            return CreateValue(key, valueFactory);
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

        private V CreateValue(K key, Func<K, V> valueFactory)
        {
            var init = initializer;

            if (init != null)
            {
                value = init.CreateValue(key, valueFactory);
                initializer = null;
            }

            return value;
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isInitialized;
            private V value;

            public V CreateValue(K key, Func<K, V> valueFactory)
            {
                if (Volatile.Read(ref isInitialized))
                {
                    return value;
                }

                lock (syncLock)
                {
                    if (Volatile.Read(ref isInitialized))
                    {
                        return value;
                    }

                    value = valueFactory(key);
                    Volatile.Write(ref isInitialized, true);
                    return value;
                }
            }
        }
    }
}
