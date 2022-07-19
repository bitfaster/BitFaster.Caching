using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

namespace BitFaster.Caching.UnitTests
{
    public class Disposable : IDisposable
    {
        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            this.IsDisposed.Should().BeFalse();
            IsDisposed = true;
        }
    }
}
