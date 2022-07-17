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
    public class ScopedCacheTests
    {
        private const int capacity = 6;
        private readonly ScopedCache<int, Disposable> cache = new (new ConcurrentLru<int, Scoped<Disposable>>(capacity));

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ScopedCache<int, Disposable>(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenCreatedCapacityPropertyWrapsInnerCache()
        {
            this.cache.Capacity.Should().Be(capacity);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            this.cache.Count.Should().Be(0);

            this.cache.AddOrUpdate(1, new Disposable());

            this.cache.Count.Should().Be(1);
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            var d = new Disposable();
            this.cache.AddOrUpdate(1, d);

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
            lifetime.Value.Should().Be(d);
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdateUpdatesExistingItem()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();
            this.cache.AddOrUpdate(1, d1);
            this.cache.AddOrUpdate(1, d2);

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
            lifetime.Value.Should().Be(d2);
        }

        [Fact]
        public void WhenItemUpdatedOldValueIsAliveUntilLifetimeCompletes()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            // start a lifetime on 1
            this.cache.AddOrUpdate(1, d1);
            this.cache.ScopedTryGet(1, out var lifetime1).Should().BeTrue();

            using (lifetime1)
            {
                // replace 1
                this.cache.AddOrUpdate(1, d2);

                // cache reflects replacement
                this.cache.ScopedTryGet(1, out var lifetime2).Should().BeTrue();
                lifetime2.Value.Should().Be(d2);

                d1.IsDisposed.Should().BeFalse();
            }

            d1.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenClearedItemsAreDisposed()
        {
            var d = new Disposable();
            this.cache.AddOrUpdate(1, d);

            this.cache.Clear();

            d.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenItemExistsTryGetReturnsLifetime()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();

            lifetime.Should().NotBeNull();
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsFalse()
        {
            this.cache.ScopedTryGet(1, out var lifetime).Should().BeFalse();
        }

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
        public async Task WhenKeyDoesNotExistGetOrAddAsyncAddsValue()
        {
            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(new Scoped<Disposable>(new Disposable())));

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
        public void GetOrAddAsyncDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope)); };

            getOrAdd.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public void WhenCacheContainsValuesTrim1RemovesColdestValue()
        {
            this.cache.AddOrUpdate(0, new Disposable());
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.AddOrUpdate(2, new Disposable());

            this.cache.Trim(1);

            this.cache.ScopedTryGet(0, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse() 
        {
            this.cache.TryRemove(1).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveReturnsTrue() 
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.TryRemove(1).Should().BeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse() 
        {
            this.cache.TryUpdate(1, new Disposable()).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryUpdateReturnsTrue() 
        {
            this.cache.AddOrUpdate(1, new Disposable());

            this.cache.TryUpdate(1, new Disposable()).Should().BeTrue();
        }
    }
}
