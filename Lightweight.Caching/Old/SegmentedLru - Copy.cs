using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lightweight.Caching2
{
	public class SegmentedLru<K, V, I> where I : LruItem<K, V>
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

		private long requestHitCount;
		private long requestTotalCount;

		private readonly Func<Func<K, V>, ItemFactoryBase<K, I, V>> createItemFactory;
		private readonly ItemPolicyBase<K, V, I> itemPolicy;

		public SegmentedLru(
			int concurrencyLevel,
			int hotCapacity,
			int warmCapacity,
			int coldCapacity,
			IEqualityComparer<K> comparer,
			Func<Func<K, V>, ItemFactoryBase<K, I, V>> createItemFactory,
			ItemPolicyBase<K, V, I> itemPolicy)
		{
			this.hotCapacity = hotCapacity;
			this.warmCapacity = warmCapacity;
			this.coldCapacity = coldCapacity;

			this.hotQueue = new ConcurrentQueue<I>();
			this.warmQueue = new ConcurrentQueue<I>();
			this.coldQueue = new ConcurrentQueue<I>();

			int dictionaryCapacity = this.hotCapacity + this.warmCapacity + this.coldCapacity + 1;

			this.dictionary = new ConcurrentDictionary<K, I>(concurrencyLevel, dictionaryCapacity, comparer);
			this.createItemFactory = createItemFactory;
			this.itemPolicy = itemPolicy;
		}

		public int Count => this.hotCount + this.warmCount + this.coldCount;

		public double HitRatio => (double)requestHitCount / (double)requestTotalCount;

		public int HotCount => this.hotCount;

		public int WarmCount => this.warmCount;

		public int ColdCount => this.coldCount;

		public bool TryGet(K key, out V value)
		{
			this.requestTotalCount++;

			if (dictionary.TryGetValue(key, out var item))
			{
				if (this.itemPolicy.DiscardLookup(item))
				{
					this.dictionary.TryRemove(item.Key, out var removed);
					value = default(V);
					return false;
				}

				item.WasAccessed = true;
				value = item.Value;
				this.requestHitCount++;
				return true;
			}

			value = default(V);
			return false;
		}

		public V GetOrAdd(K key, Func<K, V> valueFactory)
		{
			this.requestTotalCount++;

			// Keep a reference to a value created by ConcurrentDictionary.GetOrAdd using itemFactory.
			// ConcurrentDictionary can invoke the factory func and discard the result if there was a race
			// and it already has a value for the same key.
			// We can detect this outcome by reference comparing the item created to the item returned.
			var itemFactory = createItemFactory(valueFactory);
			var item = this.dictionary.GetOrAdd(key, itemFactory.Create);

			if (ReferenceEquals(item, itemFactory.ItemCreated))
			{
				// If the value returned was the same reference as the value created, we added a new value
				this.hotQueue.Enqueue(item);
				Interlocked.Increment(ref hotCount);
			}
			else
			{
				// Else we effectively did a read - no new item was added to the dictionary (either itemFactory was not
				// invoked, or there was a race and the result was discarded).
				if (this.itemPolicy.DiscardLookup(item))
				{
					// This leaves an item in one of the queues that is not in the dictionary. That's OK, it will now
					// never be looked up, and will eventually be pushed out of the queue by BumpItems
					this.dictionary.TryRemove(item.Key, out var removed);
					return GetOrAdd(key, valueFactory);
				}

				item.WasAccessed = true;
				this.requestHitCount++;
			}

			BumpItems();

			return item.Value;
		}

		private void BumpItems()
		{
			// There will be races when queue count == queue capacity. Two threads may each dequeue items.
			// This will prematurely free slots for the next caller. Each thread will still only bump at most 1 item in
			// each queue, and potentially 2 in warm.
			// Since TryDequeue is thread safe, only 1 thread can dequeue each item. Thus counts and queue state will always
			// converge on correct.
			BumpHot();
			BumpWarm();
			BumpCold();
		}

		private void BumpHot()
		{
			if (this.hotCount > this.hotCapacity)
			{
				Interlocked.Decrement(ref this.hotCount);

				if (this.hotQueue.TryDequeue(out var item))
				{
					var where = this.itemPolicy.RouteHot(item);
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
			// This mitigates the scenario where warm can grow indefinitely if keys are continuously requested in the order they were added.
			// In the event of a race, n threads will drain multiple items into the cold queue, which is a benign side effect.
			int c = this.warmCount - this.warmCapacity;
			if (c > 0)
			{
				for (int i = 0; i <= c; i++)
				{
					Interlocked.Decrement(ref this.warmCount);

					if (this.warmQueue.TryDequeue(out var item))
					{
						var where = this.itemPolicy.RouteWarm(item);
						this.Move(item, where);
					}
					else
					{
						Interlocked.Increment(ref this.warmCount);
					}
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
					var where = this.itemPolicy.RouteCold(item);
					this.Move(item, where);
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

	public class LruItem<K, V>
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

	public enum ItemDestination
	{
		Warm,
		Cold,
		Remove
	}

	public abstract class ItemFactoryBase<K, I, V> where I : LruItem<K, V>
	{
		protected readonly Func<K, V> valueFactory;

		protected ItemFactoryBase(Func<K, V> valueFactory)
		{
			this.valueFactory = valueFactory;
		}

		public I ItemCreated { get; protected set; }

		public abstract I Create(K key);
	}

	public abstract class ItemPolicyBase<K, V, I> where I : LruItem<K, V>
	{
		public abstract ItemDestination RouteHot(I item);

		public abstract ItemDestination RouteWarm(I item);

		public abstract ItemDestination RouteCold(I item);

		public abstract bool DiscardLookup(I item);
	}

	public class SegmentedLruNoExpiration<K, V> : SegmentedLru<K, V, LruItem<K, V>>
	{
		public SegmentedLruNoExpiration(int concurrencyLevel, int hotCapacity, int warmCapacity, int coldCapacity, IEqualityComparer<K> comparer)
			: base(concurrencyLevel, hotCapacity, warmCapacity, coldCapacity, comparer, vf => new LruItemFactory<K, V>(vf), new NoExpirationPolicy<K, V>())
		{
		}
	}

	public class LruItemFactory<K, V> : ItemFactoryBase<K, LruItem<K, V>, V>
	{
		public LruItemFactory(Func<K, V> valueFactory)
			: base(valueFactory)
		{
		}

		public override LruItem<K, V> Create(K key)
		{
			this.ItemCreated = new LruItem<K, V>(key, valueFactory(key));
			return ItemCreated;
		}
	}

	public class NoExpirationPolicy<K, V> : ItemPolicyBase<K, V, LruItem<K, V>>
	{
		public override ItemDestination RouteHot(LruItem<K, V> item)
		{
			if (item.WasAccessed)
			{
				return ItemDestination.Warm;
			}

			return ItemDestination.Cold;
		}

		public override ItemDestination RouteWarm(LruItem<K, V> item)
		{
			if (item.WasAccessed)
			{
				return ItemDestination.Warm;
			}

			return ItemDestination.Cold;
		}

		public override ItemDestination RouteCold(LruItem<K, V> item)
		{
			if (item.WasAccessed)
			{
				return ItemDestination.Warm;
			}

			return ItemDestination.Remove;
		}

		public override bool DiscardLookup(LruItem<K, V> item)
		{
			return false;
		}
	}
}
