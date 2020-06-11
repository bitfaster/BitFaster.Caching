using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Lightweight.Caching.UnitTests
{
	public class SingletonCacheTests
	{
		[Fact]
		public void LockObjectCache_AcquireReturnsSameLock()
		{
			SingletonCache<string, object> lockObjectCache = new SingletonCache<string, object>();

			var lock1 = lockObjectCache.Acquire("Foo");
			var lock2 = lockObjectCache.Acquire("Foo");
			lock1.Value.Should().BeSameAs(lock2.Value);
			lock1.Dispose();
			lock2.Dispose();
		}

		[Fact]
		public void LockObjectCache_AcquireReleaseAcquireReturnsDifferentLock()
		{
			SingletonCache<string, object> lockObjectCache = new SingletonCache<string, object>();

			var lock1 = lockObjectCache.Acquire("Foo");
			lock1.Dispose();

			var lock2 = lockObjectCache.Acquire("Foo");
			lock2.Dispose();

			lock1.Value.Should().NotBeSameAs(lock2.Value);
		}


		[Fact]
		public void LockObjectCache_SimpleAcquireReleaseOnDifferentThreads()
		{
			SingletonCache<string, object> lockObjectCache = new SingletonCache<string, object>();

			EventWaitHandle event1 = new EventWaitHandle(false, EventResetMode.AutoReset);
			EventWaitHandle event2 = new EventWaitHandle(false, EventResetMode.AutoReset);

			SingletonCache<string, object>.Handle lock1 = null;
			SingletonCache<string, object>.Handle lock2 = null;

			Task task1 = Task.Run(() =>
			{
				event1.WaitOne();
				lock1 = lockObjectCache.Acquire("Foo");
				event2.Set();

				event1.WaitOne();
				lock1.Dispose();
				event2.Set();
			});

			Task task2 = Task.Run(() =>
			{
				event1.Set();
				event2.WaitOne();
				lock2 = lockObjectCache.Acquire("Foo");

				event1.Set();
				event2.WaitOne();
				lock2.Dispose();
			});

			Task.WaitAll(task1, task2);

			lock1.Value.Should().BeSameAs(lock2.Value);
		}

		[Fact]
		public void LockObjectCache_StressTest()
		{
			SingletonCache<string, object> lockObjectCache = new SingletonCache<string, object>();

			int maxConcurrency = 10;
			Task[] tasks = new Task[maxConcurrency];
			for (int concurrency = 0; concurrency < maxConcurrency; concurrency++)
			{
				tasks[concurrency] = Task.Run(() =>
				{
					for (int i = 0; i < 100000; i++)
					{
						using (lockObjectCache.Acquire("Foo"))
						{
						}
					}
				});
			}
			Task.WaitAll(tasks);
		}
	}
}
