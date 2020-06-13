using Lightweight.Caching.Lru;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Lightweight.Caching.UnitTests
{
    public class CacheExtensionsTests
    {
        private ConcurrentLru<int, Scoped<MemoryStream>> lru 
            = new ConcurrentLru<int, Scoped<MemoryStream>>(2, 2, EqualityComparer<int>.Default);

        [Fact]
        public void TestGettingLifetime()
        {
            using (var lifetime = lru.CreateLifetime(1, this.ValueFactory))
            {
                var x = lifetime.Value.ToArray();
            }

            // this actually looks better, but cannot retry from CreateLifetime.
            // if lifetime is immediately created, risk of a race seems low
            using (var lifetime = lru.GetOrAdd(1, this.ValueFactory).CreateLifetime())
            {
                var x = lifetime.Value.ToArray();
            }
        }

        private Scoped<MemoryStream> ValueFactory(int key)
        {
            return new Scoped<MemoryStream>(new MemoryStream());
        }
    }
}
