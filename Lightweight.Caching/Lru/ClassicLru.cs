using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
	/// <summary>
	/// LRU implementation where Lookup operations are backed by a ConcurrentDictionary and the LRU list is protected
	/// by a global lock. All list operations performed within the lock are fast O(1) operations. 
	/// </summary>
	/// <remarks>
	/// Due to the lock protecting list operations, this class may suffer lock contention under heavy load.
	/// </remarks>
	/// <typeparam name="K">The type of the key</typeparam>
	/// <typeparam name="V">The type of the value</typeparam>
	public class ClassicLru<K, V> : ICache<K, V>
	{
		private readonly int capacity;
		private readonly ConcurrentDictionary<K, LinkedListNode<LruItem>> dictionary;
		private readonly LinkedList<LruItem> linkedList = new LinkedList<LruItem>();

		private long requestHitCount;
		private long requestTotalCount;

		public ClassicLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
		{
			this.capacity = capacity;
			this.dictionary = new ConcurrentDictionary<K, LinkedListNode<LruItem>>(concurrencyLevel, this.capacity + 1, comparer);
		}

		public int Count => this.linkedList.Count;

		public double HitRatio => (double)requestHitCount / (double)requestTotalCount;

		public bool TryGet(K key, out V value)
		{
			requestTotalCount++;

			LinkedListNode<LruItem> node;
			if (dictionary.TryGetValue(key, out node))
			{
				LockAndMoveToEnd(node);
				requestHitCount++;
				value = node.Value.Value;
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

			var node = new LinkedListNode<LruItem>(new LruItem(key, valueFactory(key)));

			if (this.dictionary.TryAdd(key, node))
			{
				LinkedListNode<LruItem> first = null;

				lock (this.linkedList)
                {
                    if (linkedList.Count >= capacity)
                    {
                        first = linkedList.First;
                        linkedList.RemoveFirst();
                    }

                    linkedList.AddLast(node);
                }

				// Remove from the dictionary outside the lock. This means that the dictionary at this moment
				// contains an item that is not in the linked list. If another thread fetches this item, 
				// LockAndMoveToEnd will ignore it, since it is detached. This means we potentially 'lose' an 
				// item just as it was about to move to the back of the LRU list and be preserved. The next request
				// for the same key will be a miss. Dictionary and list are eventually consistent.
				// However, all operations inside the lock are extremely fast, so contention is minimized.
				if (first != null)
				{
					dictionary.TryRemove(first.Value.Key, out var removed);
				}

				return node.Value.Value;
			}

			return this.GetOrAdd(key, valueFactory);
		}

		public async Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
		{
			if (this.TryGet(key, out var value))
			{
				return value;
			}

			var node = new LinkedListNode<LruItem>(new LruItem(key, await valueFactory(key)));

			if (this.dictionary.TryAdd(key, node))
			{
				LinkedListNode<LruItem> first = null;

				lock (this.linkedList)
				{
					if (linkedList.Count >= capacity)
					{
						first = linkedList.First;
						linkedList.RemoveFirst();
					}

					linkedList.AddLast(node);
				}

				// Remove from the dictionary outside the lock. This means that the dictionary at this moment
				// contains an item that is not in the linked list. If another thread fetches this item, 
				// LockAndMoveToEnd will ignore it, since it is detached. This means we potentially 'lose' an 
				// item just as it was about to move to the back of the LRU list and be preserved. The next request
				// for the same key will be a miss. Dictionary and list are eventually consistent.
				// However, all operations inside the lock are extremely fast, so contention is minimized.
				if (first != null)
				{
					dictionary.TryRemove(first.Value.Key, out var removed);
				}

				return node.Value.Value;
			}

			return await this.GetOrAddAsync(key, valueFactory);
		}

		public bool TryRemove(K key)
        {
			if (dictionary.TryRemove(key, out var node))
			{
				// If the node has already been removed from the list, ignore.
				// E.g. thread A reads x from the dictionary. Thread B adds a new item, removes x from 
				// the List & Dictionary. Now thread A will try to move x to the end of the list.
				if (node.List != null)
				{
					lock (this.linkedList)
					{
						if (node.List != null)
						{
							linkedList.Remove(node);
						}
					}
				}

				return true;
			}

			return false;
        }

		// Thead A reads x from the dictionary. Thread B adds a new item. Thread A moves x to the end. Thread B now removes the new first Node (removal is atomic on both data structures).
		private void LockAndMoveToEnd(LinkedListNode<LruItem> node)
		{
			// If the node has already been removed from the list, ignore.
			// E.g. thread A reads x from the dictionary. Thread B adds a new item, removes x from 
			// the List & Dictionary. Now thread A will try to move x to the end of the list.
			if (node.List == null)
			{
				return;
			}

			lock (this.linkedList)
			{
				if (node.List == null)
				{
					return;
				}

				linkedList.Remove(node);
				linkedList.AddLast(node);
			}
		}

		private class LruItem
		{
			public LruItem(K k, V v)
			{
				Key = k;
				Value = v;
			}

			public K Key { get; }

			public V Value { get; }
		}
	}
}
