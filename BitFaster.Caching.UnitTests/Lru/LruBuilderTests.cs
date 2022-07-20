using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LruBuilderTests
    {
        [Fact]
        public void TestFastLru()
        {
            var lru = new ConcurrentLruBuilder<int, int>()
                .Build();

            lru.Should().BeOfType<FastConcurrentLru<int, int>>();
        }

        [Fact]
        public void TestMetricsLru()
        {
            var lru = new ConcurrentLruBuilder<int, int>()
                .WithMetrics()
                .Build();

            lru.Should().BeOfType<ConcurrentLru<int, int>>();
        }

        [Fact]
        public void TestFastTLru()
        {
            var lru = new ConcurrentLruBuilder<int, int>()
                .WithAbosluteExpiry(TimeSpan.FromSeconds(1))
                .Build();

            lru.Should().BeOfType<FastConcurrentTLru<int, int>>();
        }

        [Fact]
        public void TestMetricsTLru()
        {
            var lru = new ConcurrentLruBuilder<int, int>()
                 .WithAbosluteExpiry(TimeSpan.FromSeconds(1))
                 .WithMetrics()
                 .Build();

            lru.Should().BeOfType<ConcurrentTLru<int, int>>();
            lru.Capacity.Should().Be(128);
        }

        [Fact]
        public void TestScopedOnly()
        {
            var lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<ScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        [Fact]
        public void TestScopedAtomic()
        {
            var lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<ScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        [Fact]
        public void TestScopedAtomicReverse()
        {
            var lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithAtomicCreate()
                .WithScopedValues()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<ScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
        }

        [Fact]
        public void TestAtomic()
        {
            var lru = new ConcurrentLruBuilder<int, int>()
                .WithAtomicCreate()
                .WithCapacity(3)
                .Build();

            lru.Should().BeOfType<AtomicCacheDecorator<int, int>>();
        }

        [Fact]
        public void TestComparer()
        {
            var fastLru = new ConcurrentLruBuilder<string, int>()
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
        public void ScopedPOC()
        {
            // Choose from 16 combinations of Lru/TLru, Instrumented/NotInstrumented, Atomic create/not atomic create, scoped/not scoped

            // layer 1: can choose ConcurrentLru/TLru, FastConcurrentLru/FastConcurrentTLru 
            var c = new ConcurrentLru<int, AsyncAtomic<int, Scoped<Disposable>>>(3);

            // layer 2: optional atomic creation
            var atomic = new AtomicCacheDecorator<int, Scoped<Disposable>>(c);

            // layer 3: optional scoping
            IScopedCache<int, Disposable> scoped = new ScopedCache<int, Disposable>(atomic);

            using (var lifetime = scoped.ScopedGetOrAdd(1, k => new Scoped<Disposable>(new Disposable())))
            {
                var d = lifetime.Value;
            }
        }
    }
}
