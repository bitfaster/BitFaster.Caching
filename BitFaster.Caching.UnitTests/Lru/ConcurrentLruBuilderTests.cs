using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Synchronized;
using FluentAssertions;
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

            lru.Should().BeOfType<FastConcurrentLru<int, int>>();
        }

        [Fact]
        public void TestMetricsLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithMetrics()
                .Build();

            lru.Should().BeOfType<ConcurrentLru<int, int>>();
        }

        [Fact]
        public void TestFastTLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .Build();

            lru.Should().BeOfType<FastConcurrentTLru<int, int>>();
        }

        [Fact]
        public void TestMetricsTLru()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                 .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                 .WithMetrics()
                 .Build();

            lru.Should().BeOfType<ConcurrentTLru<int, int>>();
            lru.Capacity.Should().Be(128);
        }

        [Fact]
        public void AsAsyncTestFastLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .AsAsyncCache()
                .Build();

            lru.Should().BeOfType<FastConcurrentLru<int, int>>();
        }

        [Fact]
        public void AsAsyncTestMetricsLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithMetrics()
                .AsAsyncCache()
                .Build();

            lru.Should().BeOfType<ConcurrentLru<int, int>>();
        }

        [Fact]
        public void AsAsyncTestFastTLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .AsAsyncCache()
                .Build();

            lru.Should().BeOfType<FastConcurrentTLru<int, int>>();
        }

        [Fact]
        public void AsAsyncTestMetricsTLru()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                 .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                 .WithMetrics()
                 .AsAsyncCache()
                 .Build();

            lru.Should().BeOfType<ConcurrentTLru<int, int>>();
            lru.Capacity.Should().Be(128);
        }


        [Fact]
        public void TestComparer()
        {
            ICache<string, int> fastLru = new ConcurrentLruBuilder<string, int>()
                .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
                .Build();

            fastLru.GetOrAdd("a", k => 1);
            fastLru.TryGet("A", out var value).Should().BeTrue();
        }

        [Fact]
        public void TestConcurrencyLevel()
        {
            var b = new ConcurrentLruBuilder<int, int>()
                .WithConcurrencyLevel(-1);

            Action constructor = () => { var x = b.Build(); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TestIntCapacity()
        {
            ICache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithCapacity(3)
                .Build();

            lru.Capacity.Should().Be(3);
        }

        [Fact]
        public void TestPartitionCapacity()
        {
            ICache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithCapacity(new FavorFrequencyPartition(6))
                .Build();

            lru.Capacity.Should().Be(6);
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
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<ScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        // 2
        [Fact]
        public void WithAtomicFactory()
        {
            ICache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<AtomicFactoryCache<int, int>>();
        }

        // 3
        [Fact]
        public void AsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
        }

        // 4
        [Fact]
        public void WithAtomicWithScope()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicCreate()
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<AtomicFactoryScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        // 5
        [Fact]
        public void WithScopedWithAtomic()
        {
            IScopedCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<AtomicFactoryScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        // 6
        [Fact]
        public void AsAsyncWithScoped()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsAsyncCache()
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();

            lru.Capacity.Should().Be(3);
        }

        // 7
        [Fact]
        public void WithScopedAsAsync()
        {
            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .AsAsyncCache()           
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        // 8
        [Fact]
        public void WithAtomicAsAsync()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .WithAtomicCreate()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
        }

        // 9
        [Fact]
        public void AsAsyncWithAtomic()
        {
            IAsyncCache<int, int> lru = new ConcurrentLruBuilder<int, int>()
                .AsAsyncCache()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IAsyncCache<int, int>>();
        }

        // 10
        [Fact]
        public void WithAtomicWithScopedAsAsync()
        {
            // TODO: this will not resolve a TLru

            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicCreate()
                .WithScopedValues()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 11
        [Fact]
        public void WithAtomicAsAsyncWithScoped()
        {
            // TODO: this will not resolve a TLru

            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicCreate()
                .AsAsyncCache()
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 12
        [Fact]
        public void WithScopedWithAtomicAsAsync()
        {
            // TODO: this will not resolve a TLru

            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .WithAtomicCreate()
                .AsAsyncCache()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 13
        [Fact]
        public void WithScopedAsAsyncWithAtomic()
        {
            // TODO: this will not resolve a TLru

            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .AsAsyncCache()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 14
        [Fact]
        public void AsAsyncWithScopedWithAtomic()
        {
            // TODO: this will not resolve a TLru

            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsAsyncCache()
                .WithScopedValues()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }

        // 15
        [Fact]
        public void AsAsyncWithAtomicWithScoped()
        {
            // TODO: this will not resolve a TLru

            IScopedAsyncCache<int, Disposable> lru = new ConcurrentLruBuilder<int, Disposable>()
                .AsAsyncCache()
                .WithAtomicCreate()
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeAssignableTo<IScopedAsyncCache<int, Disposable>>();
        }
    }
}
