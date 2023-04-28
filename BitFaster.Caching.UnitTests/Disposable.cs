using System;
using FluentAssertions;

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
            this.IsDisposed.Should().BeFalse();
            IsDisposed = true;
        }
    }
}
