using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching
{
    public class Lifetime<T> : IDisposable
    {
        private readonly Action onDisposeAction;
        private bool isDisposed;

        public Lifetime(T value, Action onDisposeAction)
        {
            this.Value = value;
            this.onDisposeAction = onDisposeAction;
        }

        public T Value { get; }

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
