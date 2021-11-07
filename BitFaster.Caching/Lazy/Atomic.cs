using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    // Should this be called simply Atomic?
    // Then we have Atomic and Scoped, and ScopedAtomic
    // Then AtomicAsync, ScopedAsync, and ScopedAtomicAsync would follow, but there is no ScopedAsync equivalent at this point. That should be IAsyncDisposable (.net Core 3.1 onwards). That would imply that GetOrAdd async understands IAsyncDispose

    // https://github.com/dotnet/runtime/issues/27421
    // https://github.com/alastairtree/LazyCache/issues/73
    public class Atomic<T>
    {
        private readonly Func<T> valueFactory;

        private T value;

        private bool isInitialized;

        private object @lock;

        public Atomic(Func<T> factory)
        {
            valueFactory = factory;
        }

        public T Value => LazyInitializer.EnsureInitialized(ref value, ref isInitialized, ref @lock, valueFactory);

        public bool IsValueCreated => Volatile.Read(ref isInitialized);
    }

    public class AtomicAsync<T>
    {
        private readonly Func<Task<T>> valueFactory;
    
        private Task<T> task;

        private bool isInitialized;

        private object @lock;

        public AtomicAsync(Func<Task<T>> factory)
        {
            valueFactory = factory;
        }

        public async Task<T> Value()
        {
            try
            {
                return await LazyInitializer.EnsureInitialized(ref task, ref isInitialized, ref @lock, valueFactory).ConfigureAwait(false);
            }
            catch
            {
                Volatile.Write(ref isInitialized, false);
                throw;
            }
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return Value().GetAwaiter();
        }

        public bool IsValueCreated => Volatile.Read(ref isInitialized);
    }
}
