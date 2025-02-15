﻿using System;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruBuilderTests
    {
        [Fact]
        public void TestFastLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .Build();

            lru.ShouldBeOfType<FastConcurrentLru<int, int>>();
        }

        [Fact]
        public void TestMetricsLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithMetrics()
                .Build();

            lru.ShouldBeOfType<ConcurrentLru<int, int>>();
        }

        [Fact]
        public void TestFastTLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .Build();

            lru.ShouldBeOfType<FastConcurrentTLru<int, int>>();
        }

        [Fact]
        public void TestMetricsTLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                 .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                 .WithMetrics()
                 .Build();

            lru.ShouldBeOfType<ConcurrentTLru<int, int>>();
            lru.Policy.Eviction.Value.Capacity.ShouldBe(128);
        }

        [Fact]
        public void AsAsyncTestFastLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .AsAsyncCache()
                .Build();

            lru.ShouldBeOfType<FastConcurrentLru<int, int>>();
        }

        [Fact]
        public void AsAsyncTestMetricsLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithMetrics()
                .AsAsyncCache()
                .Build();

            lru.ShouldBeOfType<ConcurrentLru<int, int>>();
        }

        [Fact]
        public void AsAsyncTestFastTLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .AsAsyncCache()
                .Build();

            lru.ShouldBeOfType<FastConcurrentTLru<int, int>>();
        }

        [Fact]
        public void AsAsyncTestMetricsTLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                 .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                 .WithMetrics()
                 .AsAsyncCache()
                 .Build();

            lru.ShouldBeOfType<ConcurrentTLru<int, int>>();
            lru.Policy.Eviction.Value.Capacity.ShouldBe(128);
        }

        [Fact]
        public void TestComparer()
        {
            ICache<string, int> fastLru = new ConcurrentLruBuilder<string, int>()
                .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
                .Build();

            fastLru.GetOrAdd("a", k => 1);
            fastLru.TryGet("A", out var value).ShouldBeTrue();
        }

        [Fact]
        public void TestConcurrencyLevel()
        {
            var b = new ConcurrentLruBuilder<int, int>()
                .WithConcurrencyLevel(-1);

            Action constructor = () => { var x = b.Build(); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TestIntCapacity()
        {
            ICache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithCapacity(3)
                .Build();

            lru.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        [Fact]
        public void TestPartitionCapacity()
        {
            ICache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithCapacity(new FavorWarmPartition(6))
                .Build();

            lru.Policy.Eviction.Value.Capacity.ShouldBe(6);
        }

        [Fact]
        public void TestExpireAfterAccess()
        {
            ICache<string, int> expireAfterAccess = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .Build();

            expireAfterAccess.Metrics.HasValue.ShouldBeFalse();
            expireAfterAccess.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterAccess.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestExpireAfterAccessWithMetrics()
        {
            ICache<string, int> expireAfterAccess = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithMetrics()
                .Build();

            expireAfterAccess.Metrics.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterAccess.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestExpireAfterReadAndExpireAfterWriteThrows()
        {
            var builder = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithExpireAfterWrite(TimeSpan.FromSeconds(2));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestExpireAfter()
        {
            ICache<string, int> expireAfter = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)))
                .Build();

            expireAfter.Metrics.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfter.HasValue.ShouldBeTrue();

            expireAfter.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestAsyncExpireAfter()
        {
            IAsyncCache<string, int> expireAfter = new ConcurrentLruBuilder<string, int>()
                .AsAsyncCache()
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)))
                .Build();

            expireAfter.Metrics.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfter.HasValue.ShouldBeTrue();

            expireAfter.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestExpireAfterWithMetrics()
        {
            ICache<string, int> expireAfter = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)))
                .WithMetrics()
                .Build();

            expireAfter.Metrics.HasValue.ShouldBeTrue();
            expireAfter.Policy.ExpireAfter.HasValue.ShouldBeTrue();

            expireAfter.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestExpireAfterWriteAndExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestExpireAfterAccessAndExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestExpireAfterAccessAndWriteAndExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestScopedWithExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, Disposable>()               
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsScopedCache();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestScopedAtomicWithExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsScopedCache()
                .WithAtomicGetOrAdd();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestAsyncScopedWithExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsAsyncCache()
                .AsScopedCache();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestAsyncScopedAtomicWithExpireAfterThrows()
        {
            var builder = new ConcurrentLruBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsAsyncCache()
                .AsScopedCache()
                .WithAtomicGetOrAdd();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        //  There are 15 combinations to test:
        //  -----------------------------
        //1 WithAtomic
        //2 WithScoped
        //3 AsAsync
        //
        //  -----------------------------
        //4 WithAtomic
        //  WithScoped
        //
        //5 WithScoped
        //  WithAtomic
        //
        //6 AsAsync
        //  WithScoped
        //
        //7 WithScoped
        //  AsAsync
        //
        //8 WithAtomic
        //  AsAsync
        //
        //9 AsAsync
        //  WithAtomic
        //
        //  -----------------------------
        //10 WithAtomic
        //   WithScoped
        //   AsAsync
        //
        //11 WithAtomic
        //   AsAsync
        //   WithScoped
        //
        //12 WithScoped
        //   WithAtomic
        //   AsAsync
        //
        //13 WithScoped
        //   AsAsync
        //   WithAtomic
        //
        //14 AsAsync
        //   WithScoped
        //   WithAtomic
        //
        //15 AsAsync
        //   WithAtomic
        //   WithScoped

        // 1
        [Fact]
        public void WithScopedValues()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeOfType<ScopedCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        // 2
        [Fact]
        public void WithAtomicFactory()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeOfType<AtomicFactoryCache<int, int>>();
        }

        // 3
        [Fact]
        public void AsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IAsyncCache<int, int>>();
        }

        // 4
        [Fact]
        public void WithAtomicWithScope()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicGetOrAdd()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeOfType<AtomicFactoryScopedCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        // 5
        [Fact]
        public void WithScopedWithAtomic()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsScopedCache()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeOfType<AtomicFactoryScopedCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        // 6
        [Fact]
        public void AsAsyncWithScoped()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsAsyncCache()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();

            lru.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        // 7
        [Fact]
        public void WithScopedAsAsync()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsScopedCache()
                .AsAsyncCache()           
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        // 8
        [Fact]
        public void WithAtomicAsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithAtomicGetOrAdd()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IAsyncCache<int, int>>();
        }

        // 9
        [Fact]
        public void AsAsyncWithAtomic()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .AsAsyncCache()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IAsyncCache<int, int>>();
        }

        // 10
        [Fact]
        public void WithAtomicWithScopedAsAsync()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicGetOrAdd()
                .AsScopedCache()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 11
        [Fact]
        public void WithAtomicAsAsyncWithScoped()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicGetOrAdd()
                .AsAsyncCache()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 12
        [Fact]
        public void WithScopedWithAtomicAsAsync()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsScopedCache()
                .WithAtomicGetOrAdd()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 13
        [Fact]
        public void WithScopedAsAsyncWithAtomic()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsScopedCache()
                .AsAsyncCache()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 14
        [Fact]
        public void AsAsyncWithScopedWithAtomic()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsAsyncCache()
                .AsScopedCache()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 15
        [Fact]
        public void AsAsyncWithAtomicWithScoped()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsAsyncCache()
                .WithAtomicGetOrAdd()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }
    }
}
