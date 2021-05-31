using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedCacheExtensionsTests
    {
        [Fact]
        public void WhenCacheReturnsDisposedValueNewValueIsRequested()
        {
            var lru = new ConcurrentLru<int, Scoped<Disposable>>(2, 9, EqualityComparer<int>.Default);
            var valueFactory = new DisposableValueFactory();

            valueFactory.ScopedDisposables.Add(new Scoped<Disposable>(new Disposable()));
            valueFactory.ScopedDisposables.Add(new Scoped<Disposable>(new Disposable()));

            valueFactory.ScopedDisposables[0].Dispose();

            // This will infinite loop because the disposed Scope is in the cache (value factory never called)
            // Therefore, it is better to encapsulate Scoped, so that a caller cannot break it.
            using (var lifetime = lru.ScopedGetOrAdd(1, valueFactory.Create))
            {
                lifetime.Value.IsDisposed.Should().BeFalse();
            }
        }

        private class DisposableValueFactory
        {
            int count;

            public List<Scoped<Disposable>> ScopedDisposables { get; } = new List<Scoped<Disposable>>();

            public Scoped<Disposable> Create(int key)
            {
                return ScopedDisposables[count++];
            }
        }

        private class Disposable : IDisposable
        {
            public bool IsDisposed { get; set; }

            public void Dispose()
            {
                this.IsDisposed.Should().BeFalse();
                IsDisposed = true;
            }
        }
    }
}
