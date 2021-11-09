using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    public class AtomicLifetime<K, V> : IDisposable where V : IDisposable
    {
        private readonly Action onDisposeAction;
        private readonly ReferenceCount<DisposableAtomic<K, V>> refCount;
        private bool isDisposed;

        public AtomicLifetime(ReferenceCount<DisposableAtomic<K, V>> refCount, Action onDisposeAction)
        {
            this.refCount = refCount;
            this.onDisposeAction = onDisposeAction;
        }

        public V Value => this.refCount.Value.ValueIfCreated;

        public int ReferenceCount => this.refCount.Count;

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
