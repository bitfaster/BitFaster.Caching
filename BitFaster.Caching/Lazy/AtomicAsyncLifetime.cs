using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
#if NETCOREAPP3_1_OR_GREATER
    public class AtomicAsyncLifetime<T> : IAsyncDisposable
    {
        private readonly Func<Task> onDisposeAction;
        private readonly ReferenceCount<AtomicAsync<T>> refCount;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the AsyncLazyLifetime class.
        /// </summary>
        /// <param name="value">The value to keep alive.</param>
        /// <param name="onDisposeAction">The action to perform when the lifetime is terminated.</param>
        public AtomicAsyncLifetime(ReferenceCount<AtomicAsync<T>> value, Func<Task> onDisposeAction)
        {
            this.refCount = value;
            this.onDisposeAction = onDisposeAction;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public Task<T> Task
        {
            get { return this.refCount.Value.Value(); } 
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return Task.GetAwaiter();
        }

        /// <summary>
        /// Gets the count of Lifetime instances referencing the same value.
        /// </summary>
        public int ReferenceCount => this.refCount.Count;

        /// <summary>
        /// Terminates the lifetime and performs any cleanup required to release the value.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!this.isDisposed)
            {
                await this.onDisposeAction();
                this.isDisposed = true;
            }
        }
    }
#endif
}
