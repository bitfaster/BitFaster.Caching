using System;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class ConcurrentLfuBuilderTests
    {
        [Fact]
        public void TestConcurrencyLevel()
        {
            var b = new ConcurrentLfuBuilder<int, int>()
                .WithConcurrencyLevel(-1);

            Action constructor = () => { var x = b.Build(); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TestIntCapacity()
        {
            ICache<int, int> lfu = new ConcurrentLfuBuilder<int, int>()
                .WithCapacity(3)
                .Build();

            lfu.Policy.Eviction.Value.Capacity.ShouldBe(3);
        }

        [Fact]
        public void TestScheduler()
        {
            ICache<int, int> lfu = new ConcurrentLfuBuilder<int, int>()
                .WithScheduler(new NullScheduler())
                .Build();

            var clfu = lfu as ConcurrentLfu<int, int>;
            clfu.Scheduler.ShouldBeOfType<NullScheduler>();
        }

        [Fact]
        public void TestComparer()
        {
            ICache<string, int> lfu = new ConcurrentLfuBuilder<string, int>()
                .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
                .Build();

            lfu.GetOrAdd("a", k => 1);
            lfu.TryGet("A", out var value).ShouldBeTrue();
        }

        [Fact]
        public void TestExpireAfterAccess()
        {
            ICache<string, int> expireAfterAccess = new ConcurrentLfuBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .Build();

            expireAfterAccess.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterAccess.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestExpireAfterReadAndExpireAfterWriteThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithExpireAfterWrite(TimeSpan.FromSeconds(2));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestExpireAfter()
        {
            ICache<string, int> expireAfter = new ConcurrentLfuBuilder<string, int>()
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)))
                .Build();

            expireAfter.Policy.ExpireAfter.HasValue.ShouldBeTrue();

            expireAfter.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestAsyncExpireAfter()
        {
            IAsyncCache<string, int> expireAfter = new ConcurrentLfuBuilder<string, int>()
                .AsAsyncCache()
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)))
                .Build();

            expireAfter.Policy.ExpireAfter.HasValue.ShouldBeTrue();

            expireAfter.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
            expireAfter.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }


        [Fact]
        public void TestExpireAfterWriteAndExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestExpireAfterAccessAndExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, int>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestExpireAfterAccessAndWriteAndExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithExpireAfter(new TestExpiryCalculator<string, int>((k, v) => Duration.FromMinutes(5)));

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestScopedWithExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsScopedCache();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestScopedAtomicWithExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsScopedCache()
                .WithAtomicGetOrAdd();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestAsyncScopedWithExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsAsyncCache()
                .AsScopedCache();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestAsyncScopedAtomicWithExpireAfterThrows()
        {
            var builder = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfter(new TestExpiryCalculator<string, Disposable>((k, v) => Duration.FromMinutes(5)))
                .AsAsyncCache()
                .AsScopedCache()
                .WithAtomicGetOrAdd();

            Action act = () => builder.Build();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void TestScopedWithExpireAfterWrite()
        {
            var expireAfterWrite = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .AsScopedCache()
                .Build();

            expireAfterWrite.Policy.ExpireAfterWrite.HasValue.ShouldBeTrue();
            expireAfterWrite.Policy.ExpireAfterWrite.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterWrite.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestScopedWithExpireAfterAccess()
        {
            var expireAfterAccess = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .AsScopedCache()
                .Build();

            expireAfterAccess.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterAccess.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestAtomicWithExpireAfterWrite()
        {
            var expireAfterWrite = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .WithAtomicGetOrAdd()
                .Build();

            expireAfterWrite.Policy.ExpireAfterWrite.HasValue.ShouldBeTrue();
            expireAfterWrite.Policy.ExpireAfterWrite.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterWrite.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestAtomicWithExpireAfterAccess()
        {
            var expireAfterAccess = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .WithAtomicGetOrAdd()
                .Build();

            expireAfterAccess.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterAccess.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestScopedAtomicWithExpireAfterWrite()
        {
            var expireAfterWrite = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .AsScopedCache()
                .WithAtomicGetOrAdd()
                .Build();

            expireAfterWrite.Policy.ExpireAfterWrite.HasValue.ShouldBeTrue();
            expireAfterWrite.Policy.ExpireAfterWrite.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterWrite.Policy.ExpireAfterAccess.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void TestScopedAtomicWithExpireAfterAccess()
        {
            var expireAfterAccess = new ConcurrentLfuBuilder<string, Disposable>()
                .WithExpireAfterAccess(TimeSpan.FromSeconds(1))
                .AsScopedCache()
                .WithAtomicGetOrAdd()
                .Build();

            expireAfterAccess.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
            expireAfterAccess.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
            expireAfterAccess.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        // 1
        [Fact]
        public void WithScopedValues()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            ICache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeOfType<AtomicFactoryCache<int, int>>();
        }

        // 3
        [Fact]
        public void AsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IAsyncCache<int, int>>();
        }

        // 4
        [Fact]
        public void WithAtomicWithScope()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IAsyncCache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
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
            IAsyncCache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
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
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
                .AsAsyncCache()
                .WithAtomicGetOrAdd()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.ShouldBeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }
    }
}
