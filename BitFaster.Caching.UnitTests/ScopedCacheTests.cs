using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        [Fact]
        public void ScopedGetOrAddDoesNotCaptureLambdaState()
        {
            typeof(ScopedCache<int, Disposable>)
                .GetNestedTypes(BindingFlags.NonPublic)
                .Select(x => x.Name)
                .Should()
                .NotContain(name => name.Contains("DisplayClass", StringComparison.Ordinal));
        }
    }
}
