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
    public class Atom<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public Atom()
        {
            this.initializer = new Initializer();
        }

        public Atom(V value)
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
