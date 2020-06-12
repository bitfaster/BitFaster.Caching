using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
	public class ConcurrentLruTemplate<K, V, I, P, H> : ICache<K, V>
		where I : LruItem<K, V>
		where P : struct, IPolicy<K, V, I>
		where H : struct, IHitCounter
	{
		private readonly ConcurrentDictionary<K, I> dictionary;

		private readonly ConcurrentQueue<I> hotQueue;
		private readonly ConcurrentQueue<I> warmQueue;
		private readonly ConcurrentQueue<I> coldQueue;

		// maintain count outside ConcurrentQueue, since ConcurrentQueue.Count holds a global lock
		private int hotCount;
		private int warmCount;
		private int coldCount;

		private readonly int hotCapacity;
		private readonly int warmCapacity;
		private readonly int coldCapacity;

		private readonly P policy;

		// Since H is a struct, making it readonly will force the runtime to make defensive copies
		// if mutate methods are called. Therefore, field must be mutable to maintain count.
		private H hitCounter;

		public ConcurrentLruTemplate(
			int concurrencyLevel,
			int capacity,
			IEqualityComparer<K> comparer,
			P itemPolicy,
			H hitCounter)
		{
			this.hotCapacity = capacity / 3;
			this.warmCapacity = capacity / 3;
			this.coldCapacity = capacity / 3;

			this.hotQueue = new ConcurrentQueue<I>();
			this.warmQueue = new ConcurrentQueue<I>();
			this.coldQueue = new ConcurrentQueue<I>();

			int dictionaryCapacity = this.hotCapacity + this.warmCapacity + this.coldCapacity + 1;

			this.dictionary = new ConcurrentDictionary<K, I>(concurrencyLevel, dictionaryCapacity, comparer);
			this.policy = itemPolicy;
			this.hitCounter = hitCounter;
		}

		public int Count => this.hotCount + this.warmCount + this.coldCount;

		public double HitRatio => this.hitCounter.HitRatio;

		public int HotCount => this.hotCount;

		public int WarmCount => this.warmCount;

		public int ColdCount => this.coldCount;

		public bool TryGet(K key, out V value)
		{
			this.hitCounter.IncrementTotalCount();

			I item;
			if (dictionary.TryGetValue(key, out item))
			{
				if (this.policy.ShouldDiscard(item))
				{
					this.dictionary.TryRemove(item.Key, out var removed);
					value = default(V);
					return false;
				}

				value = item.Value;
				this.policy.Touch(item);
				this.hitCounter.IncrementHitCount();
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
			var newItem = this.policy.CreateItem(key, valueFactory(key));

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
			var newItem = this.policy.CreateItem(key, await valueFactory(key).ConfigureAwait(false));

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
					var where = this.policy.RouteHot(item);
					this.Move(item, where);
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
					var where = this.policy.RouteWarm(item);

					// When the warm queue is full, we allow an overflow of 1 item before redirecting warm items to cold.
					// This only happens when hit rate is high, in which case we can consider all items relatively equal in
					// terms of which was least recently used.
					if (where == ItemDestination.Warm && this.warmCount <= this.warmCapacity)
					{
						this.Move(item, where);
					}
					else
					{
						this.Move(item, ItemDestination.Cold);
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
					var where = this.policy.RouteCold(item);

					if (where == ItemDestination.Warm && this.warmCount <= this.warmCapacity)
					{
						this.Move(item, where);
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

		private void Move(I item, ItemDestination where)
		{
			item.WasAccessed = false;

			switch (where)
			{
				case ItemDestination.Warm:
					this.warmQueue.Enqueue(item);
					Interlocked.Increment(ref this.warmCount);
					break;
				case ItemDestination.Cold:
					this.coldQueue.Enqueue(item);
					Interlocked.Increment(ref this.coldCount);
					break;
				case ItemDestination.Remove:
					this.dictionary.TryRemove(item.Key, out var removedItem);
					break;
			}
		}
	}
}
