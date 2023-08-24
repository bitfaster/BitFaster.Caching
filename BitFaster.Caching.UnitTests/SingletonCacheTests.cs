using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class SingletonCacheTests
    {
        [Fact]
        public void WhenItemAddedAndRemovedCounIsCorrect()
        {
            var cache = new SingletonCache<int, object>();
            cache.Count.Should().Be(0);

            var lifetime1 = cache.Acquire(1);
            cache.Count.Should().Be(1);

            var lifetime2 = cache.Acquire(2);
            cache.Count.Should().Be(2);

            lifetime1.Dispose();
            cache.Count.Should().Be(1);

            lifetime2.Dispose();
            cache.Count.Should().Be(0);
        }

        [Fact]
        public void AcquireWithSameKeyUsingCustomComparerReturnsSameLifetime()
        {
            var cache = new SingletonCache<string, object>(1, 3, StringComparer.OrdinalIgnoreCase);

            var lifetime1 = cache.Acquire("foo");
            var lifetime2 = cache.Acquire("FOO");
            lifetime1.Value.Should().BeSameAs(lifetime2.Value);
            lifetime1.Dispose();
            lifetime2.Dispose();
        }

        [Fact]
        public void AcquireWithSameKeyReturnsSameLifetime()
        {
            var cache = new SingletonCache<string, object>();

            var lifetime1 = cache.Acquire("Foo");
            var lifetime2 = cache.Acquire("Foo");
            lifetime1.Value.Should().BeSameAs(lifetime2.Value);
            lifetime1.Dispose();
            lifetime2.Dispose();
        }

        [Fact]
        public void AcquireWithSameKeyMaintainsReferenceCount()
        {
            var cache = new SingletonCache<string, object>();

            using (var lifetime1 = cache.Acquire("Foo"))
            {
                using (var lifetime2 = cache.Acquire("Foo"))
                {
                    lifetime1.ReferenceCount.Should().Be(1);
                    lifetime2.ReferenceCount.Should().Be(2);
                }

                using (var lifetime3 = cache.Acquire("Foo"))
                {
                    lifetime3.ReferenceCount.Should().Be(2);
                }
            }

            using (var lifetime4 = cache.Acquire("Foo"))
            {
                lifetime4.ReferenceCount.Should().Be(1);
            }
        }

        [Fact]
        public void AcquireReleaseAcquireReturnsDifferentValue()
        {
            var cache = new SingletonCache<string, object>();

            var lifetime1 = cache.Acquire("Foo");
            lifetime1.Dispose();

            var lifetime2 = cache.Acquire("Foo");
            lifetime2.Dispose();

            lifetime1.Value.Should().NotBeSameAs(lifetime2.Value);
        }

        [Fact]
        public async Task AcquireWithSameKeyOnTwoDifferentThreadsReturnsSameValue()
        {
            var cache = new SingletonCache<string, object>();

            EventWaitHandle event1 = new EventWaitHandle(false, EventResetMode.AutoReset);
            EventWaitHandle event2 = new EventWaitHandle(false, EventResetMode.AutoReset);

            Lifetime<object> lifetime1 = null;
            Lifetime<object> lifetime2 = null;

            Task task1 = Task.Run(() =>
            {
                event1.WaitOne();
                lifetime1 = cache.Acquire("Foo");
                event2.Set();

                event1.WaitOne();
                lifetime1.Dispose();
                event2.Set();
            });

            Task task2 = Task.Run(() =>
            {
                event1.Set();
                event2.WaitOne();
                lifetime2 = cache.Acquire("Foo");

                event1.Set();
                event2.WaitOne();
                lifetime2.Dispose();
            });

            await Task.WhenAll(task1, task2);

            lifetime1.Value.Should().BeSameAs(lifetime2.Value);
        }

        [Fact]
        public async Task AcquireWithSameKeyOnManyDifferentThreadsReturnsSameValue()
        {
            int count = 0;

            var cache = new SingletonCache<string, object>();

            int maxConcurrency = Environment.ProcessorCount + 1;
            Task[] tasks = new Task[maxConcurrency];
            for (int concurrency = 0; concurrency < maxConcurrency; concurrency++)
            {
                tasks[concurrency] = Task.Run(() =>
                {
                    for (int i = 0; i < 100_000; i++)
                    {
                        using (var lifetime = cache.Acquire("Foo"))
                        {
                            lock (lifetime.Value)
                            {
                                int result = Interlocked.Increment(ref count);
                                result.Should().Be(1);
                                Interlocked.Decrement(ref count);
                            }
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public void WhenValueIsDisposableItIsDisposedWhenReleased()
        {
            var cache = new SingletonCache<string, DisposeTest>();

            DisposeTest value = null;

            using (var lifetime = cache.Acquire("Foo"))
            {
                value = lifetime.Value;
                value.IsDisposed.Should().BeFalse();
            }

            value.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public async Task DisposeOnManyDifferentThreadsAlwaysReturnsActiveValue()
        {
            var cache = new SingletonCache<string, DisposeTest>();

            var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() => 
            { 
                using (var handle = cache.Acquire("Foo"))
                { 
                    handle.Value.ThrowIfDisposed();    
                }
            }));

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task WhenSoakConcurrentGetCacheEndsInConsistentState()
        {
            var cache = new SingletonCache<int, DisposeTest>();

            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        using (var handle = cache.Acquire(i))
                        {
                            handle.Value.ThrowIfDisposed();
                        }
                    }
                });

                cache.Count.Should().Be(0);
            }
        }

        public class DisposeTest : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void ThrowIfDisposed()
            {
                if (this.IsDisposed)
                {
                    throw new ObjectDisposedException("Error");
                }
            }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }
    }
}
