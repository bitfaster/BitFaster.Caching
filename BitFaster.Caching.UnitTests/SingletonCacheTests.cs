using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
	public class SingletonCacheTests
	{
		[Fact]
		public void AcquireWithSameKeyUsingCustomComparerReturnsSameHandle()
		{
			var cache = new SingletonCache<string, object>(1, 3, StringComparer.OrdinalIgnoreCase);

			var handle1 = cache.Acquire("foo");
			var handle2 = cache.Acquire("FOO");
			handle1.Value.Should().BeSameAs(handle2.Value);
			handle1.Dispose();
			handle2.Dispose();
		}

		[Fact]
		public void AcquireWithSameKeyReturnsSameHandle()
		{
			var cache = new SingletonCache<string, object>();

			var handle1 = cache.Acquire("Foo");
			var handle2 = cache.Acquire("Foo");
			handle1.Value.Should().BeSameAs(handle2.Value);
			handle1.Dispose();
			handle2.Dispose();
		}

		[Fact]
		public void AcquireReleaseAcquireReturnsDifferentValue()
		{
			var cache = new SingletonCache<string, object>();

			var handle1 = cache.Acquire("Foo");
			handle1.Dispose();

			var handle2 = cache.Acquire("Foo");
			handle2.Dispose();

			handle1.Value.Should().NotBeSameAs(handle2.Value);
		}

		[Fact]
		public async Task AcquireWithSameKeyOnTwoDifferentThreadsReturnsSameValue()
		{
			var cache = new SingletonCache<string, object>();

			EventWaitHandle event1 = new EventWaitHandle(false, EventResetMode.AutoReset);
			EventWaitHandle event2 = new EventWaitHandle(false, EventResetMode.AutoReset);

			SingletonCache<string, object>.Handle handle1 = null;
			SingletonCache<string, object>.Handle handle2 = null;

			Task task1 = Task.Run(() =>
			{
				event1.WaitOne();
				handle1 = cache.Acquire("Foo");
				event2.Set();

				event1.WaitOne();
				handle1.Dispose();
				event2.Set();
			});

			Task task2 = Task.Run(() =>
			{
				event1.Set();
				event2.WaitOne();
				handle2 = cache.Acquire("Foo");

				event1.Set();
				event2.WaitOne();
				handle2.Dispose();
			});

			await Task.WhenAll(task1, task2);

			handle1.Value.Should().BeSameAs(handle2.Value);
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
					for (int i = 0; i < 100000; i++)
					{
						using (var handle = cache.Acquire("Foo"))
						{
							lock (handle.Value)
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

			using (var handle = cache.Acquire("Foo"))
			{
				DisposeTest.WasDisposed.Should().BeFalse();
			}

			DisposeTest.WasDisposed.Should().BeTrue();
		}

		public class DisposeTest : IDisposable
        {
			public static bool WasDisposed { get; set; }

            public void Dispose()
            {
				WasDisposed = true;
            }
        }
	}
}
