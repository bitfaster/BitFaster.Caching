using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedCacheTests : ScopedCacheTestBase
    {
        public ScopedCacheTests()
            : base(new ScopedCache<int, Disposable>(new ConcurrentLru<int, Scoped<Disposable>>(capacity)))
        {
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ScopedCache<int, Disposable>(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void ComparerReturnsConfiguredComparer()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, comparer));

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

#if NET9_0_OR_GREATER
        [Fact]
        public void TryGetAlternateLookupReturnsLookupForCompatibleComparer()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
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
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));

            Action act = () => cache.GetAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void AlternateLookupTryRemoveReturnsActualKey()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
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
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
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
        public void WhenScopeIsDisposedTryGetAltReturnsFalse()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            var scope = new Scoped<Disposable>(new Disposable());

            cache.ScopedGetOrAdd("a", k => scope);

            scope.Dispose();

            alternate.ScopedTryGet("a", out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveAltReturnsTrue()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            cache.AddOrUpdate("a", new Disposable());
            alternate.TryRemove("a", out var key).Should().BeTrue();
            key.Should().Be("a");
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetAltReturnsFalse()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            alternate.ScopedTryGet("a", out _).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveAltReturnsFalse()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            alternate.TryRemove("a", out _).Should().BeFalse();
        }

        [Fact]
        public void GetOrAddAltDisposedScopeThrows()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Action getOrAdd = () => { alternate.ScopedGetOrAdd("a", k => scope); };

            getOrAdd.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
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
            var cache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
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
}
