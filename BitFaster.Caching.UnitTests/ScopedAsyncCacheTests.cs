using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedAsyncCacheTests : ScopedAsyncCacheTestBase
    {
        public ScopedAsyncCacheTests()
            : base(new ScopedAsyncCache<int, Disposable>(new ConcurrentLru<int, Scoped<Disposable>>(capacity)))
        {
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ScopedAsyncCache<int, Disposable>(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void ComparerReturnsConfiguredComparer()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, comparer));

            cache.Comparer.Should().BeSameAs(comparer);
        }

        [Fact]
        public async Task TryGetAsyncAlternateLookupReturnsLookupForCompatibleComparer()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            using var lifetime = await cache.ScopedGetOrAddAsync("42", _ => Task.FromResult(new Scoped<Disposable>(new Disposable(42))));
            ReadOnlySpan<char> key = "42";

            cache.TryGetAsyncAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.ScopedTryGet(key, out var alternateLifetime).Should().BeTrue();
            alternateLifetime.Value.State.Should().Be(42);
            alternateLifetime.Dispose();
        }

        [Fact]
        public void GetAsyncAlternateLookupThrowsForIncompatibleComparer()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));

            Action act = () => cache.GetAsyncAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAsyncAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public async Task AsyncAlternateLookupTryRemoveReturnsActualKey()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            using var lifetime = await cache.ScopedGetOrAddAsync("42", _ => Task.FromResult(new Scoped<Disposable>(new Disposable(42))));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out var actualKey).Should().BeTrue();

            actualKey.Should().Be("42");
            cache.ScopedTryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public async Task AsyncAlternateLookupScopedGetOrAddAsyncUsesActualKeyOnMissAndHit()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            var key = "42";

            using var lifetime = await alternate.ScopedGetOrAddAsync(key.AsSpan(), k =>
            {
                factoryCalls++;
                return Task.FromResult(new Scoped<Disposable>(new Disposable(int.Parse(k))));
            });

            using var sameLifetime = await alternate.ScopedGetOrAddAsync(key.AsSpan(), (k, offset) =>
            {
                factoryCalls++;
                return Task.FromResult(new Scoped<Disposable>(new Disposable(int.Parse(k) + offset)));
            }, 1);

            lifetime.Value.State.Should().Be(42);
            sameLifetime.Value.State.Should().Be(42);
            factoryCalls.Should().Be(1);
        }

        [Fact]
        public async Task WhenScopeIsDisposedTryGetAsyncAltReturnsFalse()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var scope = new Scoped<Disposable>(new Disposable());

            await cache.ScopedGetOrAddAsync("a", _ => Task.FromResult(scope));

            scope.Dispose();

            alternate.ScopedTryGet("a", out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveAsyncAltReturnsTrue()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();

            cache.AddOrUpdate("a", new Disposable());
            alternate.TryRemove("a", out var key).Should().BeTrue();
            key.Should().Be("a");
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetAsyncAltReturnsFalse()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            alternate.ScopedTryGet("a", out _).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveAsyncAltReturnsFalse()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            alternate.TryRemove("a", out _).Should().BeFalse();
        }

        [Fact]
        public async Task GetOrAddAsyncAltDisposedScopeThrows()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();

            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await alternate.ScopedGetOrAddAsync("a", _ => Task.FromResult(scope)); };

            await getOrAdd.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task AsyncAlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var key = "42";

            alternate.TryUpdate(key.AsSpan(), new Disposable(42)).Should().BeFalse();
            cache.ScopedTryGet("42", out _).Should().BeFalse();

            using var lifetime = await cache.ScopedGetOrAddAsync("42", _ => Task.FromResult(new Scoped<Disposable>(new Disposable(1))));
            lifetime.Dispose();

            alternate.TryUpdate(key.AsSpan(), new Disposable(2)).Should().BeTrue();

            alternate.ScopedTryGet(key.AsSpan(), out var updatedLifetime).Should().BeTrue();
            updatedLifetime.Value.State.Should().Be(2);
            updatedLifetime.Dispose();
        }

        [Fact]
        public void AsyncAlternateLookupAddOrUpdateAddsMissingValueAndUpdatesExistingValue()
        {
            var cache = new ScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
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

        [Fact]
        public async Task WhenScopeIsDisposedTryGetReturnsFalse()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope));

            scope.Dispose();

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public async Task WhenKeyDoesNotExistGetOrAddAsyncAddsValue()
        {
            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(new Scoped<Disposable>(new Disposable())));

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
        }

        [Fact]
        public async Task GetOrAddAsyncDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope)); };

            await getOrAdd.Should().ThrowAsync<InvalidOperationException>();
        }

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public async Task GetOrAddAsyncArgDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, (k, a) => Task.FromResult(scope), 2); };

            await getOrAdd.Should().ThrowAsync<InvalidOperationException>();
        }
#endif
    }
}
