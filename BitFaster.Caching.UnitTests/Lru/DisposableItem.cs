using System;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class DisposableItem : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            this.IsDisposed = true;
        }
    }
}
