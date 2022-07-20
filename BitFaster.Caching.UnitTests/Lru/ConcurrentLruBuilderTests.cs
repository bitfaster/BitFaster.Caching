﻿using System;
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
                .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                .Build();

            lru.Should().BeOfType<FastConcurrentTLru<int, int>>();
        }

        [Fact]
        public void TestMetricsTLru()
        {
            var lru = new ConcurrentLruBuilder<int, int>()
                 .WithExpireAfterWrite(TimeSpan.FromSeconds(1))
                 .WithMetrics()
                 .Build();

            lru.Should().BeOfType<ConcurrentTLru<int, int>>();
            lru.Capacity.Should().Be(128);
        }

        [Fact]
        public void TestScoped()
        {
            var lru = new ConcurrentLruBuilder<int, Disposable>()
                .WithScopedValues()
                .WithCapacity(3)
                .WithExpireAfterWrite(TimeSpan.FromMinutes(1))
                .Build();

            lru.Should().BeOfType<ScopedCache<int, Disposable>>();
            lru.Capacity.Should().Be(3);
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
    }
}
