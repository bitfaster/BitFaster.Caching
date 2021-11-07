using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    public class LazyLifetime<T> : IDisposable
    {
        private readonly Action onDisposeAction;
        private readonly ReferenceCount<AtomicLazy<T>> refCount;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the Lifetime class.
        /// </summary>
        /// <param name="value">The value to keep alive.</param>
        /// <param name="onDisposeAction">The action to perform when the lifetime is terminated.</param>
        public LazyLifetime(ReferenceCount<AtomicLazy<T>> value, Action onDisposeAction)
        {
            this.refCount = value;
            this.onDisposeAction = onDisposeAction;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public T Value => this.refCount.Value.Value;

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
