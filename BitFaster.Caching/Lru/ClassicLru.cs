using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
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
    public sealed class ClassicLru<K, V> : ICache<K, V>, IAsyncCache<K, V>, IBoundedPolicy, IEnumerable<KeyValuePair<K, V>>
    {
        private readonly int capacity;
        private readonly ConcurrentDictionary<K, LinkedListNode<LruItem>> dictionary;
        private readonly LinkedList<LruItem> linkedList = new LinkedList<LruItem>();

        private readonly CacheMetrics metrics = new CacheMetrics();
        private readonly CacheEvents events = new CacheEvents();
        private readonly CachePolicy policy;

        public ClassicLru(int capacity)
            : this(Defaults.ConcurrencyLevel, capacity, EqualityComparer<K>.Default)
        {
        }

        public ClassicLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
        {
            if (capacity < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            this.capacity = capacity;
            this.dictionary = new ConcurrentDictionary<K, LinkedListNode<LruItem>>(concurrencyLevel, this.capacity + 1, comparer);
            this.policy = new CachePolicy(this, Optional<ITimePolicy>.None());
        }

        ///<inheritdoc/>
        public int Count => this.linkedList.Count;

        ///<inheritdoc/>
        public int Capacity => this.capacity;

        ///<inheritdoc/>
        public ICacheMetrics Metrics => this.metrics;

        public ICacheEvents<K, V> Events => this.events;

        public CachePolicy Policy => this.policy;

        /// <summary>
        /// Gets a collection containing the keys in the cache.
        /// </summary>
        public ICollection<K> Keys => this.dictionary.Keys;

        /// <summary>Returns an enumerator that iterates through the cache.</summary>
        /// <returns>An enumerator for the cache.</returns>
        /// <remarks>
        /// The enumerator returned from the cache is safe to use concurrently with
        /// reads and writes, however it does not represent a moment-in-time snapshot.  
        /// The contents exposed through the enumerator may contain modifications
        /// made after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.dictionary)
            {
                yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.Value.Value);
            }
        }

        ///<inheritdoc/>
        public bool TryGet(K key, out V value)
        {
            Interlocked.Increment(ref this.metrics.requestTotalCount);

            if (dictionary.TryGetValue(key, out var node))
            {
                LockAndMoveToEnd(node);
                Interlocked.Increment(ref this.metrics.requestHitCount);
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        ///<inheritdoc/>
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

                    Interlocked.Increment(ref this.metrics.evictedCount);
                    Disposer<V>.Dispose(removed.Value.Value);
                }

                return node.Value.Value;
            }

            return this.GetOrAdd(key, valueFactory);
        }

        ///<inheritdoc/>
        public async ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
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

                    Interlocked.Increment(ref this.metrics.evictedCount);
                    Disposer<V>.Dispose(removed.Value.Value);
                }

                return node.Value.Value;
            }

            return await this.GetOrAddAsync(key, valueFactory);
        }

        ///<inheritdoc/>
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

                Disposer<V>.Dispose(node.Value.Value);

                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        ///<remarks>Note: Calling this method does not affect LRU order.</remarks>
        public bool TryUpdate(K key, V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                node.Value.Value = value;
                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        ///<remarks>Note: Updates to existing items do not affect LRU order. Added items are at the top of the LRU.</remarks>
        public void AddOrUpdate(K key, V value)
        {
            // first, try to update
            if (this.dictionary.TryGetValue(key, out var existingNode))
            {
                existingNode.Value.Value = value;
                return;
            }

            // then try add
            var newNode = new LinkedListNode<LruItem>(new LruItem(key, value));

            if (this.dictionary.TryAdd(key, newNode))
            {
                LinkedListNode<LruItem> first = null;

                lock (this.linkedList)
                {
                    if (linkedList.Count >= capacity)
                    {
                        first = linkedList.First;
                        linkedList.RemoveFirst();
                    }

                    linkedList.AddLast(newNode);
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

                    Interlocked.Increment(ref this.metrics.evictedCount);
                    Disposer<V>.Dispose(removed.Value.Value);
                }

                return;
            }

            // if both update and add failed there was a race, try again
            AddOrUpdate(key, value);
        }

        ///<inheritdoc/>
        public void Clear()
        {
            // take a key snapshot
            var keys = this.dictionary.Keys.ToList();

            // remove all keys in the snapshot - this correctly handles disposable values
            foreach (var key in keys)
            {
                TryRemove(key);
            }
        }

        ///<inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemCount"/> is less than 0./</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemCount"/> is greater than capacity./</exception>
        public void Trim(int itemCount)
        {
            if (itemCount < 1 || itemCount > this.capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(itemCount), "itemCount must be greater than or equal to one, and less than the capacity of the cache.");
            }

            for (int i = 0; i < itemCount; i++)
            {
                LinkedListNode<LruItem> first = null;

                lock (this.linkedList)
                {
                    if (linkedList.Count > 0)
                    {
                        first = linkedList.First;
                        linkedList.RemoveFirst();
                    }
                }

                if (first != null)
                {
                    dictionary.TryRemove(first.Value.Key, out var removed);
                    Disposer<V>.Dispose(removed.Value.Value);
                }
            }
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

        /// <summary>Returns an enumerator that iterates through the cache.</summary>
        /// <returns>An enumerator for the cache.</returns>
        /// <remarks>
        /// The enumerator returned from the cache is safe to use concurrently with
        /// reads and writes, however it does not represent a moment-in-time snapshot.  
        /// The contents exposed through the enumerator may contain modifications
        /// made after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ClassicLru<K, V>)this).GetEnumerator();
        }

        private class LruItem
        {
            public LruItem(K k, V v)
            {
                Key = k;
                Value = v;
            }

            public K Key { get; }

            public V Value { get; set; }
        }

        private class CacheMetrics : ICacheMetrics
        {
            public long requestHitCount;
            public long requestTotalCount;
            public long evictedCount;

            public double HitRatio => (double)requestHitCount / (double)requestTotalCount;

            public long Total => requestTotalCount;

            public long Hits => requestHitCount;

            public long Misses => requestTotalCount - requestHitCount;

            public long Evicted => evictedCount;

            public bool IsEnabled => true;
        }

        private class CacheEvents : ICacheEvents<K, V>
        {
            public bool IsEnabled => false;

#pragma warning disable CS0067 // The event 'event' is never used
            public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
            {
                // no-op, nothing is registered
                add { }
                remove { }
            }
#pragma warning restore CS0067 // The event 'event' is never used
        }
    }
}
