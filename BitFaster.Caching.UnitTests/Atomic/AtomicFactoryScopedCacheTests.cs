using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Moq;
using Xunit;

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

            constructor.Should().Throw<ArgumentNullException>();
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void ComparerReturnsConfiguredComparer()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, comparer));

            cache.Comparer.Should().BeSameAs(comparer);
        }
#endif

        [Fact]
        public void WhenScopeIsDisposedTryGetReturnsFalse()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            this.cache.ScopedGetOrAdd(1, k => scope);

            scope.Dispose();

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistGetOrAddAddsValue()
        {
            this.cache.ScopedGetOrAdd(1, k => new Scoped<Disposable>(new Disposable()));

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
        }

        [Fact]
        public void GetOrAddDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Action getOrAdd = () => { this.cache.ScopedGetOrAdd(1, k => scope); };

            getOrAdd.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void WhenNoInnerEventsNoOuterEvents()
        {
            var inner = new Mock<ICache<int, ScopedAtomicFactory<int, Disposable>>>();
            inner.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<int, ScopedAtomicFactory<int, Disposable>>>.None());

            var cache = new AtomicFactoryScopedCache<int, Disposable>(inner.Object);

            cache.Events.HasValue.Should().BeFalse();
        }

        // Infer identified AddOrUpdate and TryUpdate as resource leaks. This test verifies correct disposal.
        [Fact]
        public void WhenEntryIsUpdatedOldEntryIsDisposed()
        {
            var disposable1 = new Disposable();
            var disposable2 = new Disposable();

            this.cache.AddOrUpdate(1, disposable1);

            this.cache.TryUpdate(1, disposable2).Should().BeTrue();
            disposable1.IsDisposed.Should().BeTrue();

            this.cache.TryUpdate(1, new Disposable()).Should().BeTrue();
            disposable2.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenFactoryThrowsEmptyValueIsNotCounted()
        {
            try
            {
                cache.ScopedGetOrAdd(1, _ => throw new Exception());
            }
            catch { }

            cache.Count.Should().Be(0);
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
            cache.Count().Should().Be(0);
        }

        [Fact]
        public void WhenFactoryThrowsEmptyKeyIsNotEnumerable()
        {
            try
            {
                cache.ScopedGetOrAdd(1, k => throw new Exception());
            }
            catch { }

            cache.Keys.Count().Should().Be(0);
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void TryGetAlternateLookupReturnsLookupForCompatibleComparer()
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            using var lifetime = cache.ScopedGetOrAdd("42", _ => new Scoped<Disposable>(new Disposable(42)));
            ReadOnlySpan<char> key = "42";

            cache.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.ScopedTryGet(key, out var alternateLifetime).Should().BeTrue();
            alternateLifetime.Value.State.Should().Be(42);
            alternateLifetime.Dispose();
        }

        [Fact]
        public void GetAlternateLookupThrowsForIncompatibleComparer()
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));

            Action act = () => cache.GetAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void AlternateLookupTryRemoveReturnsActualKey()
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            using var lifetime = cache.ScopedGetOrAdd("42", _ => new Scoped<Disposable>(new Disposable(42)));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out var actualKey).Should().BeTrue();

            actualKey.Should().Be("42");
            cache.ScopedTryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public void AlternateLookupScopedGetOrAddUsesActualKeyOnMissAndHit()
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            ReadOnlySpan<char> key = "42";

            using var lifetime = alternate.ScopedGetOrAdd(key, k =>
            {
                factoryCalls++;
                return new Scoped<Disposable>(new Disposable(int.Parse(k)));
            });

            using var sameLifetime = alternate.ScopedGetOrAdd(key, (k, offset) =>
            {
                factoryCalls++;
                return new Scoped<Disposable>(new Disposable(int.Parse(k) + offset));
            }, 1);

            lifetime.Value.State.Should().Be(42);
            sameLifetime.Value.State.Should().Be(42);
            factoryCalls.Should().Be(1);
        }

        [Fact]
        public void AlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryUpdate(key, new Disposable(42)).Should().BeFalse();
            cache.ScopedTryGet("42", out _).Should().BeFalse();

            using var lifetime = cache.ScopedGetOrAdd("42", _ => new Scoped<Disposable>(new Disposable(1)));
            lifetime.Dispose();

            alternate.TryUpdate(key, new Disposable(2)).Should().BeTrue();

            alternate.ScopedTryGet(key, out var updatedLifetime).Should().BeTrue();
            updatedLifetime.Value.State.Should().Be(2);
            updatedLifetime.Dispose();
        }

        [Fact]
        public void AlternateLookupAddOrUpdateAddsMissingValueAndUpdatesExistingValue()
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.AddOrUpdate(key, new Disposable(42));
            alternate.ScopedTryGet(key, out var lifetime).Should().BeTrue();
            lifetime.Value.State.Should().Be(42);
            lifetime.Dispose();

            alternate.AddOrUpdate(key, new Disposable(43));
            alternate.ScopedTryGet(key, out var updatedLifetime).Should().BeTrue();
            updatedLifetime.Value.State.Should().Be(43);
            updatedLifetime.Dispose();
        }
#endif
    }

#if NET9_0_OR_GREATER
    [Collection("Soak")]
    public class AtomicFactoryScopedCacheSoakTests
    {
        private const int capacity = 6;
        private const int threadCount = 4;
        private const int soakIterations = 10;
        private const int loopIterations = 100_000;

        [Theory]
        [Repeat(soakIterations)]
        public async Task ScopedGetOrAddAlternateLifetimeIsAlwaysAlive(int _)
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            var run = Threaded.Run(threadCount, _ =>
            {
                var key = new char[8];

                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);

                    using (var lifetime = alternate.ScopedGetOrAdd(key.AsSpan().Slice(0, written), k => { return new Scoped<Disposable>(new Disposable(int.Parse(k))); }))
                    {
                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                    }
                }
            });

            await run;
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task ScopedGetOrAddAlternateArgLifetimeIsAlwaysAlive(int _)
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            var run = Threaded.Run(threadCount, _ =>
            {
                var key = new char[8];

                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);

                    using (var lifetime = alternate.ScopedGetOrAdd(key.AsSpan().Slice(0, written), (k, offset) => { return new Scoped<Disposable>(new Disposable(int.Parse(k) + offset)); }, 1))
                    {
                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                    }
                }
            });

            await run;
        }
    }
#endif
}
