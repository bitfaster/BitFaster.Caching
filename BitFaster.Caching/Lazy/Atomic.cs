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
        private readonly Func<T> _factory;

        private T _value;

        private bool _initialized;

        private object _lock;

        public Atomic(Func<T> factory)
        {
            _factory = factory;
        }

        public T Value => LazyInitializer.EnsureInitialized(ref _value, ref _initialized, ref _lock, _factory);

        public bool IsValueCreated => Volatile.Read(ref _initialized);
    }

    public class AtomicAsync<T>
    {
        private readonly Func<Task<T>> _factory;
    
        private Task<T> _task;

        private bool _initialized;

        private object _lock;

        public AtomicAsync(Func<Task<T>> factory)
        {
            _factory = factory;
        }

        public async Task<T> Value()
        {
            try
            {
                return await LazyInitializer.EnsureInitialized(ref _task, ref _initialized, ref _lock, _factory).ConfigureAwait(false);
            }
            catch
            {
                Volatile.Write(ref _initialized, false);
                throw;
            }
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return Value().GetAwaiter();
        }

        public bool IsValueCreated => Volatile.Read(ref _initialized);
    }
}
