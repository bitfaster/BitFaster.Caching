using System;
using Shouldly;

namespace BitFaster.Caching.UnitTests
{
    public class Disposable : IDisposable
    {
        public Disposable() { }
        
        public Disposable(int state) { this.State = state; }

        public bool IsDisposed { get; set; }

        public int State { get; set; }

        public void Dispose()
        {
            this.IsDisposed.ShouldBeFalse();
            IsDisposed = true;
        }
    }
}
