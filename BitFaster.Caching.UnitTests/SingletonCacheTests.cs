using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class SingletonCacheTests
    {
        [Fact]
        public void AcquireWithSameKeyUsingCustomComparerReturnsSameLifetime()
        {
            var cache = new SingletonCache<string, object>(1, 3, StringComparer.OrdinalIgnoreCase);

            var lifetime1 = cache.Acquire("foo");
            var lifetime2 = cache.Acquire("FOO");
            lifetime1.Value.ShouldBeSameAs(lifetime2.Value);
            lifetime1.Dispose();
            lifetime2.Dispose();
        }

        [Fact]
        public void AcquireWithSameKeyReturnsSameLifetime()
        {
            var cache = new SingletonCache<string, object>();

            var lifetime1 = cache.Acquire("Foo");
            var lifetime2 = cache.Acquire("Foo");
            lifetime1.Value.ShouldBeSameAs(lifetime2.Value);
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
                    lifetime1.ReferenceCount.ShouldBe(1);
                    lifetime2.ReferenceCount.ShouldBe(2);
                }

                using (var lifetime3 = cache.Acquire("Foo"))
                {
                    lifetime3.ReferenceCount.ShouldBe(2);
                }
            }

            using (var lifetime4 = cache.Acquire("Foo"))
            {
                lifetime4.ReferenceCount.ShouldBe(1);
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

            lifetime1.Value.ShouldNotBeSameAs(lifetime2.Value);
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

            lifetime1.Value.ShouldBeSameAs(lifetime2.Value);
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
                                result.ShouldBe(1);
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
                value.IsDisposed.ShouldBeFalse();
            }

            value.IsDisposed.ShouldBeTrue();
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
