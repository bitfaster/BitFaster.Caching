using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public class AsyncAtomicLifetime<K, V> : IDisposable
    {
        private readonly Action onDisposeAction;
        private readonly ReferenceCount<AsyncAtomic<K, V>> refCount;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the AsyncLazyLifetime class.
        /// </summary>
        /// <param name="value">The value to keep alive.</param>
        /// <param name="onDisposeAction">The action to perform when the lifetime is terminated.</param>
        public AsyncAtomicLifetime(ReferenceCount<AsyncAtomic<K, V>> value, Action onDisposeAction)
        {
            this.refCount = value;
            this.onDisposeAction = onDisposeAction;
        }

        public Task<V> GetValueAsync(K key, Func<K, Task<V>> valueFactory)
        { 
            return this.refCount.Value.GetValueAsync(key, valueFactory);
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public V Value => this.refCount.Value.ValueIfCreated;

        /// <summary>
        /// Gets the count of Lifetime instances referencing the same value.
        /// </summary>
        public int ReferenceCount => this.refCount.Count;

        /// <summary>
        /// Terminates the lifetime and performs any cleanup required to release the value.
        /// </summary>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.onDisposeAction();
                this.isDisposed = true;
            }
        }
    }
}
