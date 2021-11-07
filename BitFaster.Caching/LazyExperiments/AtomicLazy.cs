using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // https://github.com/dotnet/runtime/issues/27421
    // https://github.com/alastairtree/LazyCache/issues/73
    public class AtomicLazy<T>
    {
        private readonly Func<T> _factory;

        private T _value;

        private bool _initialized;

        private object _lock;

        public AtomicLazy(Func<T> factory)
        {
            _factory = factory;
        }

        public T Value => LazyInitializer.EnsureInitialized(ref _value, ref _initialized, ref _lock, _factory);

        public bool IsValueCreated => _initialized;
    }

    public class AtomicAsyncLazy<T>
    {
        private readonly Func<Task<T>> _factory;
    
        private Task<T> _task;

        private bool _initialized;

        private object _lock;

        public AtomicAsyncLazy(Func<Task<T>> factory)
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

        public bool IsValueCreated => _initialized;
    }
}
