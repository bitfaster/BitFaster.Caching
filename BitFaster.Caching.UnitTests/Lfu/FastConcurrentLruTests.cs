using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{

    public class FastConcurrentLfuTests : FastConcurrentLruTestBase
    {
        private static ICache<string, int> Create()
        {
            return new ConcurrentLfuBuilder<string, int>()
                .WithCapacity(expectedCapacity)
                .WithKeyComparer(expectedComparer)
                .Build();
        }

        public FastConcurrentLfuTests()
            : base(Create())
        {
        }
    }

    public class FastConcurrentLfuAfterAccessTests : FastConcurrentLruTestBase
    {
        private static ICache<string, int> Create()
        {
            return new ConcurrentLfuBuilder<string, int>()
                .WithCapacity(expectedCapacity)
                .WithExpireAfterAccess(expectedTimeToExpire)
                .WithKeyComparer(expectedComparer)
                .Build();
        }

        public FastConcurrentLfuAfterAccessTests()
            : base(Create())
        {
        }
    }

    public class FastConcurrentLfuAfterWriteTests : FastConcurrentLruTestBase
    {
        private static ICache<string, int> Create()
        {
            return new ConcurrentLfuBuilder<string, int>()
                .WithCapacity(expectedCapacity)
                .WithExpireAfterWrite(expectedTimeToExpire)
                .WithKeyComparer(expectedComparer)
                .Build();
        }

        public FastConcurrentLfuAfterWriteTests()
            : base(Create())
        {
        }
    }

    public class FastConcurrentLfuExpireAfterTests : FastConcurrentLruTestBase
    {
        private static ICache<string, int> Create()
        {
            var calc = new TestExpiryCalculator<string, int>();
            calc.ExpireAfterCreate = (k, v) => Duration.FromMinutes(1);

            return new ConcurrentLfuBuilder<string, int>()
                .WithCapacity(expectedCapacity)
                .WithExpireAfter(calc)
                .WithKeyComparer(expectedComparer)
                .Build();
        }

        public FastConcurrentLfuExpireAfterTests()
            : base(Create())
        {
        }
    }

    // Verify API surface is wired up correctly.
    // Core cache logic is tested in ConcurrentLfuTests and ConcurrentTLfuTests.
    public abstract class FastConcurrentLruTestBase
    {
        protected ICache<string, int> cache;

        protected static int expectedCapacity = 128;
        protected static TimeSpan expectedTimeToExpire = TimeSpan.FromSeconds(1);
        protected static IEqualityComparer<string> expectedComparer = StringComparer.OrdinalIgnoreCase;

        protected FastConcurrentLruTestBase(ICache<string, int> cache)
        {
            this.cache = cache;
        }

        [Fact]
        public void CountReturnsItemCount()
        {
            this.cache.Count().Should().Be(0);

            this.cache.AddOrUpdate("foo", 1);

            this.cache.Count().Should().Be(1);
        }

        [Fact]
        public void EventsAreNotEnabled()
        {
            this.cache.Events.HasValue.Should().BeFalse();
        }

        [Fact]
        public void MetricsHasValueIsTrue()
        {
            this.cache.Metrics.HasValue.Should().BeTrue();
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void ComparerReturnsConfiguredComparer()
        {
            this.cache.Comparer.Should().BeSameAs(expectedComparer);
        }
#endif

        [Fact]
        public void CapacityReturnsCapacity()
        {
            if (IsTimeOrder())
            {
                AsTimeOrder()
                    .Capacity.Should().Be(expectedCapacity);
            }
            else
            {
                AsAccessOrder()
                    .Capacity.Should().Be(expectedCapacity);
            }
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            cache.Count.Should().Be(0);
            cache.GetOrAdd("1", k => 1);
            cache.GetOrAdd("2", k => 1);

            var enumerable = (IEnumerable)cache;
            enumerable.Should().BeEquivalentTo(new[] { new KeyValuePair<string, int>("1", 1), new KeyValuePair<string, int>("2", 1) });
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            cache.Count.Should().Be(0);
            cache.GetOrAdd("1", k => 1);
            cache.GetOrAdd("2", k => 1);
            cache.Keys.Should().BeEquivalentTo(["1", "2"]);
        }

        [Fact]
        public void DefaultSchedulerIsThreadPool()
        {
            if (IsTimeOrder())
            {
                AsTimeOrder()
                    .Scheduler.Should().BeOfType<ThreadPoolScheduler>();
            }
            else
            {
                AsAccessOrder()
                   .Scheduler.Should().BeOfType<ThreadPoolScheduler>();
            }
        }

        [Fact]
        public void TimeToLiveReturnsExpected()
        {
            if (IsTimeOrder())
            {
                var timeOrder = AsTimeOrder();
                var expected = expectedTimeToExpire;

                if (timeOrder.Policy.ExpireAfter.HasValue)
                {
                    expected = TimeSpan.Zero;
                }

                (timeOrder as ITimePolicy).TimeToLive.Should().Be(expected);
            }
            else
            {
                (AsAccessOrder() as ITimePolicy).TimeToLive.Should().Be(TimeSpan.Zero);
            }
        }

        [Fact]
        public void WhenKeyExistsTryGetTimeToExpireReturnsExpiry()
        {
            if (IsTimeOrder() && AsTimeOrder().Policy.ExpireAfter.HasValue)
            {
                cache.GetOrAdd("1", k => 1);

                cache.Policy.ExpireAfter.Value.TryGetTimeToExpire("1", out var timeToExpire).Should().BeTrue();
                timeToExpire.Should().BeCloseTo(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(50));
            }
        }

        [Fact]
        public void WhenKeyDoesNotExistTryGetTimeToExpireReturnsFalse()
        {
            if (IsTimeOrder() && AsTimeOrder().Policy.ExpireAfter.HasValue)
            {
                cache.Policy.ExpireAfter.Value.TryGetTimeToExpire("1", out _).Should().BeFalse();
            }
            else
            {
                (cache as IDiscreteTimePolicy).TryGetTimeToExpire("1", out _).Should().BeFalse();
            }
        }

        [Fact]
        public void WhenKeyTypeMismatchTryGetTimeToExpireReturnsFalse()
        {
            if (IsTimeOrder() && AsTimeOrder().Policy.ExpireAfter.HasValue)
            {
                cache.Policy.ExpireAfter.Value.TryGetTimeToExpire(123, out _).Should().BeFalse();
            }
        }

        [Fact]
        public void WhenCacheIsClearedCountIsZero()
        {
            this.cache.Count().Should().Be(0);

            this.cache.AddOrUpdate("foo", 1);
            this.cache.AddOrUpdate("bar", 2);

            this.cache.Clear();

            this.cache.Count().Should().Be(0);
        }

        [Fact]
        public void GetOrAddAddsItem()
        {
            cache.GetOrAdd("foo", k => 1);

            cache.TryGet("foo", out var value).Should().BeTrue();
            value.Should().Be(1);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void GetOrAddArgAddsItem()
        {
            cache.GetOrAdd("foo", (k, a) => a, 2);

            cache.TryGet("foo", out var value).Should().BeTrue();
            value.Should().Be(2);
        }
#endif

        [Fact]
        public async Task GetOrAddAsyncAddsItem()
        {
            await (cache as IAsyncCache<string, int>).GetOrAddAsync("foo", k => Task.FromResult(2));

            cache.TryGet("foo", out var value).Should().BeTrue();
            value.Should().Be(2);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public async Task GetOrAddAsyncArgAddsItem()
        {
            await (cache as IAsyncCache<string, int>).GetOrAddAsync("foo", (k, a) => Task.FromResult(a), 2);

            cache.TryGet("foo", out var value).Should().BeTrue();
            value.Should().Be(2);
        }
#endif

        [Fact]
        public void WhenKeyExistsTryRemoveRemovesItem()
        {
            cache.GetOrAdd("1", k => 1);

            cache.TryRemove("1").Should().BeTrue();
            cache.TryGet("1", out _).Should().BeFalse();
        }

        [Fact]
        public void WhenItemDoesNotExistTryUpdateIsFalse()
        {
            cache.TryUpdate("1", 2).Should().BeFalse();
        }

#if DEBUG
        [Fact]
        public void FormatLfuReturnsExpectedString()
        {
            cache.GetOrAdd("foo", k => 1);
            cache.GetOrAdd("bar", k => 1);

            string expected = "W [foo,bar] Protected [] Probation []";

            if (IsTimeOrder())
            {
                var c = AsTimeOrder();
                c.DoMaintenance();
                c.FormatLfuString().Should().Be(expected);
            }
            else
            {
                var c = AsAccessOrder();
                c.DoMaintenance();
                c.FormatLfuString().Should().Be(expected);
            }
        }
#endif

#if NET9_0_OR_GREATER
        [Fact]
        public void GetAlternateLookupReturnsLookupForCompatibleComparer()
        {
            cache.GetOrAdd("42", _ => 123);

            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            alternate.TryGet("42".AsSpan(), out var value).Should().BeTrue();
            value.Should().Be(123);
        }

        [Fact]
        public void TryGetAlternateLookupReturnsTrueForCompatibleComparer()
        {
            cache.GetOrAdd("42", _ => 123);

            cache.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.TryGet("42".AsSpan(), out var value).Should().BeTrue();
            value.Should().Be(123);
        }

        [Fact]
        public void GetAsyncAlternateLookupReturnsLookupForCompatibleComparer()
        {
            cache.GetOrAdd("42", _ => 123);

            var alternate = (cache as IAsyncCache<string, int>).GetAsyncAlternateLookup<ReadOnlySpan<char>>();

            alternate.TryGet("42".AsSpan(), out var value).Should().BeTrue();
            value.Should().Be(123);
        }

        [Fact]
        public void TryGetAsyncAlternateLookupReturnsTrueForCompatibleComparer()
        {
            cache.GetOrAdd("42", _ => 123);

            (cache as IAsyncCache<string, int>).TryGetAsyncAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.TryGet("42".AsSpan(), out var value).Should().BeTrue();
            value.Should().Be(123);
        }
#endif

        private bool IsTimeOrder()
        {
            return this.cache is FastConcurrentLfu<string, int, TimeOrderNode<string, int>, ExpireAfterPolicy<string, int, NoEventPolicy<string, int>>>;
        }

        private FastConcurrentLfu<string, int, TimeOrderNode<string, int>, ExpireAfterPolicy<string, int, NoEventPolicy<string, int>>> AsTimeOrder()
        {
            return this.cache as FastConcurrentLfu<string, int, TimeOrderNode<string, int>, ExpireAfterPolicy<string, int, NoEventPolicy<string, int>>>;
        }

        private FastConcurrentLfu<string, int, AccessOrderNode<string, int>, AccessOrderPolicy<string, int, NoEventPolicy<string, int>>> AsAccessOrder()
        {
            return this.cache as FastConcurrentLfu<string, int, AccessOrderNode<string, int>, AccessOrderPolicy<string, int, NoEventPolicy<string, int>>>;
        }
    }
}
