using System;
using System.Collections.Generic;
using System.Text;

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
