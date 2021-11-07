using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.LazyExperiments
{
    public class AsyncLazy<T>
    {
        private readonly object mutex;
        private Lazy<Task<T>> lazy;
        private readonly Func<Task<T>> valueFactory;

        public AsyncLazy(Func<Task<T>> valueFactory)
        {
            this.mutex = new object();
            this.valueFactory = RetryOnFailure(valueFactory);
            this.lazy = new Lazy<Task<T>>(this.valueFactory);
        }

        private Func<Task<T>> RetryOnFailure(Func<Task<T>> valueFactory)
        {
            return async () =>
            {
                try
                {
                    return await valueFactory().ConfigureAwait(false);
                }
                catch
                {
                    // lock exists only because lazy is replaced on error
                    // better approach might be either:
                    // - value factory is responsible for retry, not lazy (single responsibility)
                    // - use TLRU, expire invalid items
                    lock (mutex)
                    {
                        this.lazy = new Lazy<Task<T>>(this.valueFactory);
                    }
                    throw;
                }
            };
        }

        public Task<T> Task
        {
            get { lock (this.mutex) { return this.lazy.Value; } }
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return Task.GetAwaiter();
        }

        public bool IsValueCreated
        { get;set;}
    }
}
