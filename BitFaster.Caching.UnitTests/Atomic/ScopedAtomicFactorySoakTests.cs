using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    [Collection("Soak")]
    public class ScopedAtomicFactorySoakTests
    {
        [Fact]
        public async Task WhenGetOrAddIsConcurrentValuesCreatedAtomically()
        {
            const int threads = 4;
            const int items = 1024;
            var dictionary = new ConcurrentDictionary<int, ScopedAtomicFactory<int, Disposable>>(concurrencyLevel: threads, capacity: items);
            var counters = new int[threads];

            await Threaded.Run(threads, (r) =>
            {
                for (int i = 0; i < items; i++)
                {
                    while (true)
                    {
                        var scoped = dictionary.GetOrAdd(i, k => new ScopedAtomicFactory<int, Disposable>());
                        if (scoped.TryCreateLifetime(i, k => { counters[r]++; return new Scoped<Disposable>(new Disposable(k)); }, out var lifetime))
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
            const int threads = 4;
            const int items = 1024;
            var dictionary = new ConcurrentDictionary<int, ScopedAtomicFactory<int, Disposable>>(concurrencyLevel: threads, capacity: items);

            await Threaded.Run(threads, (r) =>
            {
                for (int i = 0; i < items; i++)
                {
                    if (dictionary.TryRemove(i, out var d))
                    {
                        d.Dispose();
                    }

                    while (true)
                    {
                        var scoped = dictionary.GetOrAdd(i, k => new ScopedAtomicFactory<int, Disposable>());

                        if (scoped.TryCreateLifetime(i, k => { return new Scoped<Disposable>(new Disposable(k)); }, out var lifetime))
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
