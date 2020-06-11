using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lightweight.Caching
{
	/// <summary>
	/// LRU implementation where LRU list is composed of 3 segments: hot, warm and cold. Cost of maintaining
	/// segments is amortized across requests. Items are only bumped when capacity is exceeded. Pure read does
	/// not bump items if all segments are within capacity constraints.
	/// There are no global locks. On cache miss, a new item is added. Tail items in each segment are dequeued,
	/// examined, and are either enqueued or discarded.
	/// </summary>
	/// <remarks>
	/// Each segment has a capacity. When segment capacity is exceeded, items are moved as follows:
	/// 1. New items are added to hot, WasAccessed = false
	/// 2. When items are accessed, update WasAccessed = true
	/// 3. When items are moved WasAccessed is set to false.
	/// 4. When hot is full, hot tail is moved to either Warm or Cold depending on WasAccessed. 
	/// 5. When warm is full, warm tail is moved to warm head or cold depending on WasAccessed.
	/// 6. When cold is full, cold tail is moved to warm head or removed from dictionary on depending on WasAccessed.
	/// </remarks>
	/// <typeparam name="K">The type of the key</typeparam>
	/// <typeparam name="V">The type of the value</typeparam>
	public class SegmentedLru<K, V> : ICache<K, V>
	{
		private readonly ConcurrentDictionary<K, LruItem> dictionary;

		private readonly ConcurrentQueue<LruItem> hotQueue;
		private readonly ConcurrentQueue<LruItem> warmQueue;
		private readonly ConcurrentQueue<LruItem> coldQueue;

		// maintain count outside ConcurrentQueue, since ConcurrentQueue.Count holds a global lock
		private int hotCount;
		private int warmCount;
		private int coldCount;

		private readonly int hotCapacity;
		private readonly int warmCapacity;
		private readonly int coldCapacity;

		private long requestHitCount;
		private long requestTotalCount;

		public SegmentedLru(int concurrencyLevel, int hotCapacity, int warmCapacity, int coldCapacity, IEqualityComparer<K> comparer)
		{
			this.hotCapacity = hotCapacity;
			this.warmCapacity = warmCapacity;
			this.coldCapacity = coldCapacity;

			this.hotQueue = new ConcurrentQueue<LruItem>();
			this.warmQueue = new ConcurrentQueue<LruItem>();
			this.coldQueue = new ConcurrentQueue<LruItem>();

			int dictionaryCapacity = this.hotCapacity + this.warmCapacity + this.coldCapacity + 1;

			this.dictionary = new ConcurrentDictionary<K, LruItem>(concurrencyLevel, dictionaryCapacity, comparer);
		}

		public int Count => this.hotCount + this.warmCount + this.coldCount;

		public double HitRatio => (double)requestHitCount / (double)requestTotalCount;

		public int HotCount => this.hotCount;

		public int WarmCount => this.warmCount;

		public int ColdCount => this.coldCount;

		public bool TryGet(K key, out V value)
		{
			this.requestTotalCount++;

			LruItem item;
			if (dictionary.TryGetValue(key, out item))
			{
				value = item.Value;
				item.WasAccessed = true;
				this.requestHitCount++;
				return true;
			}

			value = default(V);
			return false;
		}

		public V GetOrAdd(K key, Func<K, V> valueFactory)
		{
			if (this.TryGet(key, out var value))
			{
				return value;
			}

			// The value factory may be called concurrently for the same key, but the first write to the dictionary wins.
			// This is identical logic to the ConcurrentDictionary.GetOrAdd method.
			var newItem = new LruItem(key, valueFactory(key));

			if (this.dictionary.TryAdd(key, newItem))
			{
				this.hotQueue.Enqueue(newItem);
				Interlocked.Increment(ref hotCount);
				BumpItems();
				return newItem.Value;
			}

			return this.GetOrAdd(key, valueFactory);
		}

		public async Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
		{
			if (this.TryGet(key, out var value))
			{
				return value;
			}

			// The value factory may be called concurrently for the same key, but the first write to the dictionary wins.
			// This is identical logic to the ConcurrentDictionary.GetOrAdd method.
			var newItem = new LruItem(key, await valueFactory(key).ConfigureAwait(false));

			if (this.dictionary.TryAdd(key, newItem))
			{
				this.hotQueue.Enqueue(newItem);
				Interlocked.Increment(ref hotCount);
				BumpItems();
				return newItem.Value;
			}

			return await this.GetOrAddAsync(key, valueFactory).ConfigureAwait(false);
		}

		private void BumpItems()
		{
			// There will be races when queue count == queue capacity. Two threads may each dequeue items.
			// This will prematurely free slots for the next caller. Each thread will still only bump at most 5 items.
			// Since TryDequeue is thread safe, only 1 thread can dequeue each item. Thus counts and queue state will always
			// converge on correct over time.
			BumpHot();

			// Multi-threaded stress tests show that due to races, the warm and cold count can increase beyond capacity when
			// hit rate is very high. Double bump results in stable count under all conditions. When contention is low, 
			// secondary bumps have no effect.
			BumpWarm();
			BumpWarm();
			BumpCold();
			BumpCold();
		}

		private void BumpHot()
		{
			if (this.hotCount > this.hotCapacity)
			{
				Interlocked.Decrement(ref this.hotCount);

				if (this.hotQueue.TryDequeue(out var item))
				{
					if (item.WasAccessed)
					{
						item.WasAccessed = false;
						this.warmQueue.Enqueue(item);
						Interlocked.Increment(ref this.warmCount);
					}
					else
					{
						this.coldQueue.Enqueue(item);
						Interlocked.Increment(ref this.coldCount);
					}
				}
				else
				{
					Interlocked.Increment(ref this.hotCount);
				}
			}
		}

		private void BumpWarm()
		{
			if (this.warmCount > this.warmCapacity)
			{
				Interlocked.Decrement(ref this.warmCount);

				if (this.warmQueue.TryDequeue(out var item))
				{
					// When the warm queue is full, we allow an overflow of 1 item before redirecting warm items to cold.
					// This only happens when hit rate is high, in which case we can consider all items relatively equal in
					// terms of which was least recently used.
					if (item.WasAccessed && this.warmCount <= this.warmCapacity)
					{
						item.WasAccessed = false;
						this.warmQueue.Enqueue(item);
						Interlocked.Increment(ref this.warmCount);
					}
					else
					{
						item.WasAccessed = false;
						this.coldQueue.Enqueue(item);
						Interlocked.Increment(ref this.coldCount);
					}
				}
				else
				{
					Interlocked.Increment(ref this.warmCount);
				}
			}
		}

		private void BumpCold()
		{
			if (this.coldCount > this.coldCapacity)
			{
				Interlocked.Decrement(ref this.coldCount);

				if (this.coldQueue.TryDequeue(out var item))
				{
					if (item.WasAccessed && this.warmCount <= this.warmCapacity)
					{
						item.WasAccessed = false;
						this.warmQueue.Enqueue(item);
						Interlocked.Increment(ref this.warmCount);
					}
					else
					{
						if (this.dictionary.TryRemove(item.Key, out var removedItem))
						{
							if (removedItem.Value is IDisposable d)
							{
								d.Dispose();
							}
						}
					}
				}
				else
				{
					Interlocked.Increment(ref this.coldCount);
				}
			}
		}

		private class LruItem
		{
			private volatile bool wasAccessed;

			public LruItem(K k, V v)
			{
				this.Key = k;
				this.Value = v;
			}

			public readonly K Key;

			public readonly V Value;

			public bool WasAccessed
			{
				get => this.wasAccessed;
				set => this.wasAccessed = value;
			}
		}
	}
}
