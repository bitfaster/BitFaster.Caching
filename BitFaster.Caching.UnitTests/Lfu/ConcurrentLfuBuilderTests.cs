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

        [Fact]
        public void WithAtomicFactory()
        {
            ICache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
                .WithAtomicGetOrAdd()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<AtomicFactoryCache<int, int>>();
        }

        [Fact]
        public void AsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLfuBuilder<int, int>()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
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
    }
}
