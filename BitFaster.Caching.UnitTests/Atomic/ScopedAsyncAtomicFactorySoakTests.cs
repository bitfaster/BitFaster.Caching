using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    [Collection("Soak")]
    public class ScopedAsyncAtomicFactorySoakTests
    {
        private const int threads = 4;
        private const int items = 1024;

        [Fact]
        public async Task WhenGetOrAddIsConcurrentValuesCreatedAtomically()
        {
            var dictionary = new ConcurrentDictionary<int, ScopedAsyncAtomicFactory<int, Disposable>>(concurrencyLevel: threads, capacity: items);
            var counters = new int[threads];

            await Threaded.RunAsync(threads, async (r) =>
            {
                for (int i = 0; i < items; i++)
                {
                    while (true)
                    {
                        var scope = dictionary.GetOrAdd(i, k => new ScopedAsyncAtomicFactory<int, Disposable>());
                        var (success, lifetime) = await scope.TryCreateLifetimeAsync(i, k => { counters[r]++; return Task.FromResult(new Scoped<Disposable>(new Disposable(k))); });

                        if (success)
                        {
                            using (lifetime)
                            {
                                lifetime.Value.IsDisposed.Should().BeFalse();
                            }

                            break;
                        }
                    }
                }
            });

            counters.Sum(x => x).Should().Be(items);
        }

        [Fact]
        public async Task WhenGetOrAddAndDisposeIsConcurrentLifetimesAreValid()
        {
            var dictionary = new ConcurrentDictionary<int, ScopedAsyncAtomicFactory<int, Disposable>>(concurrencyLevel: threads, capacity: items);

            await Threaded.RunAsync(threads, async (r) =>
            {
                for (int i = 0; i < items; i++)
                {
                    if (dictionary.TryRemove(i, out var d))
                    {
                        d.Dispose();
                    }

                    while (true)
                    {
                        var scope = dictionary.GetOrAdd(i, k => new ScopedAsyncAtomicFactory<int, Disposable>());
                        var (success, lifetime) = await scope.TryCreateLifetimeAsync(i, k => { return Task.FromResult(new Scoped<Disposable>(new Disposable(k))); });

                        if (success)
                        {
                            using (lifetime)
                            {
                                lifetime.Value.IsDisposed.Should().BeFalse();
                            }

                            break;
                        }
                    }
                }
            });
        }
    }
}
