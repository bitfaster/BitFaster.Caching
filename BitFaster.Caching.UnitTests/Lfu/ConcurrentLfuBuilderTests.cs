using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
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

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TestIntCapacity()
        {
            ICache<int, int> lfu = new ConcurrentLfuBuilder<int, int>()
                .WithCapacity(3)
                .Build();

            lfu.Policy.Eviction.Value.Capacity.Should().Be(3);
        }

        [Fact]
        public void TestScheduler()
        {
            ICache<int, int> lfu = new ConcurrentLfuBuilder<int, int>()
                .WithScheduler(new NullScheduler())
                .Build();

            var clfu = lfu as ConcurrentLfu<int, int>;
            clfu.Scheduler.Should().BeOfType<NullScheduler>();
        }

        [Fact]
        public void TestComparer()
        {
            ICache<string, int> lfu = new ConcurrentLfuBuilder<string, int>()
                .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
                .Build();

            lfu.GetOrAdd("a", k => 1);
            lfu.TryGet("A", out var value).Should().BeTrue();
        }

        // 1
        [Fact]
        public void WithScopedValues()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLfuBuilder<int, Disposable>()
                .AsScopedCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<ScopedCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.Should().Be(3);
        }

        // 2
        [Fact]
        public void WithAtomicFactory()
        {
            ICache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<AtomicFactoryCache<int, int>>();
        }

        // 3
        [Fact]
        public void AsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
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

            lru.Should().BeOfType<AtomicFactoryScopedCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.Should().Be(3);
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

            lru.Should().BeOfType<AtomicFactoryScopedCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.Should().Be(3);
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();

            lru.Policy.Eviction.Value.Capacity.Should().Be(3);
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
            lru.Policy.Eviction.Value.Capacity.Should().Be(3);
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

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
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

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
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

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }
    }
}
