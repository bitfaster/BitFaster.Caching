﻿using System;
using System.Linq;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;
using Moq;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryScopedCacheTests : ScopedCacheTestBase
    {
        public AtomicFactoryScopedCacheTests()
            : base(new AtomicFactoryScopedCache<int, Disposable>(new ConcurrentLru<int, ScopedAtomicFactory<int, Disposable>>(capacity)))
        {
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new AtomicFactoryScopedCache<int, Disposable>(null); };

            constructor.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenScopeIsDisposedTryGetReturnsFalse()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            this.cache.ScopedGetOrAdd(1, k => scope);

            scope.Dispose();

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistGetOrAddAddsValue()
        {
            this.cache.ScopedGetOrAdd(1, k => new Scoped<Disposable>(new Disposable()));

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeTrue();
        }

        [Fact]
        public void GetOrAddDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Action getOrAdd = () => { this.cache.ScopedGetOrAdd(1, k => scope); };

            getOrAdd.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void WhenNoInnerEventsNoOuterEvents()
        {
            var inner = new Mock<ICache<int, ScopedAtomicFactory<int, Disposable>>>();
            inner.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<int, ScopedAtomicFactory<int, Disposable>>>.None());

            var cache = new AtomicFactoryScopedCache<int, Disposable>(inner.Object);

            cache.Events.HasValue.ShouldBeFalse();
        }

        // Infer identified AddOrUpdate and TryUpdate as resource leaks. This test verifies correct disposal.
        [Fact]
        public void WhenEntryIsUpdatedOldEntryIsDisposed()
        {
            var disposable1 = new Disposable();
            var disposable2 = new Disposable();

            this.cache.AddOrUpdate(1, disposable1);

            this.cache.TryUpdate(1, disposable2).ShouldBeTrue();
            disposable1.IsDisposed.ShouldBeTrue();

            this.cache.TryUpdate(1, new Disposable()).ShouldBeTrue();
            disposable2.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenFactoryThrowsEmptyValueIsNotCounted()
        {
            try
            {
                cache.ScopedGetOrAdd(1, _ => throw new Exception());
            }
            catch { }

            cache.Count.ShouldBe(0);
        }

        [Fact]
        public void WhenFactoryThrowsEmptyValueIsNotEnumerable()
        {
            try
            {
                cache.ScopedGetOrAdd(1, k => throw new Exception());
            }
            catch { }

            // IEnumerable.Count() instead of Count property
            cache.Count().ShouldBe(0);
        }

        [Fact]
        public void WhenFactoryThrowsEmptyKeyIsNotEnumerable()
        {
            try
            {
                cache.ScopedGetOrAdd(1, k => throw new Exception());
            }
            catch { }

            cache.Keys.Count().ShouldBe(0);
        }
    }
}
