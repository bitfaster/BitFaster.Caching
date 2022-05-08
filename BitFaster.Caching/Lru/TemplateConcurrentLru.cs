using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Pseudo LRU implementation where LRU list is composed of 3 segments: hot, warm and cold. Cost of maintaining
    /// segments is amortized across requests. Items are only cycled when capacity is exceeded. Pure read does
    /// not cycle items if all segments are within capacity constraints.
    /// There are no global locks. On cache miss, a new item is added. Tail items in each segment are dequeued,
    /// examined, and are either enqueued or discarded.
    /// This scheme of hot, warm and cold is based on the implementation used in MemCached described online here:
    /// https://memcached.org/blog/modern-lru/
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
    public class TemplateConcurrentLru<K, V, I, P, T> : ICache<K, V>, IEnumerable<KeyValuePair<K, V>>
        where I : LruItem<K, V>
        where P : struct, IItemPolicy<K, V, I>
        where T : struct, ITelemetryPolicy<K, V>
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

        private readonly P itemPolicy;

        // Since H is a struct, making it readonly will force the runtime to make defensive copies
        // if mutate methods are called. Therefore, field must be mutable to maintain count.
        protected T telemetryPolicy;

        public TemplateConcurrentLru(
            int concurrencyLevel,
            int capacity,
            IEqualityComparer<K> comparer,
            P itemPolicy,
            T telemetryPolicy)
        {
            if (capacity < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            var (hot, warm, cold) = ComputeQueueCapacity(capacity);
            this.hotCapacity = hot;
            this.warmCapacity = warm;
            this.coldCapacity = cold;

            this.hotQueue = new ConcurrentQueue<I>();
            this.warmQueue = new ConcurrentQueue<I>();
            this.coldQueue = new ConcurrentQueue<I>();

            int dictionaryCapacity = this.hotCapacity + this.warmCapacity + this.coldCapacity + 1;

            this.dictionary = new ConcurrentDictionary<K, I>(concurrencyLevel, dictionaryCapacity, comparer);
            this.itemPolicy = itemPolicy;
            this.telemetryPolicy = telemetryPolicy;
            this.telemetryPolicy.SetEventSource(this);
        }

        // No lock count: https://arbel.net/2013/02/03/best-practices-for-using-concurrentdictionary/
        public int Count => this.dictionary.Skip(0).Count();

        public int HotCount => this.hotCount;

        public int WarmCount => this.warmCount;

        public int ColdCount => this.coldCount;

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
                yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.Value);
            }
        }

        ///<inheritdoc/>
        public bool TryGet(K key, out V value)
        {
            if (dictionary.TryGetValue(key, out var item))
            {
                return GetOrDiscard(item, out value);
            }

            value = default;
            this.telemetryPolicy.IncrementMiss();
            return false;
        }

        // AggressiveInlining forces the JIT to inline policy.ShouldDiscard(). For LRU policy 
        // the first branch is completely eliminated due to JIT time constant propogation.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetOrDiscard(I item, out V value)
        {
            if (this.itemPolicy.ShouldDiscard(item))
            {
                this.Move(item, ItemDestination.Remove, ItemRemovedReason.Evicted);
                this.telemetryPolicy.IncrementMiss();
                value = default;
                return false;
            }

            value = item.Value;
            this.itemPolicy.Touch(item);
            this.telemetryPolicy.IncrementHit();
            return true;
        }

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {        
                if (this.TryGet(key, out var value))
                {
                    return value;
                }

                // The value factory may be called concurrently for the same key, but the first write to the dictionary wins.
                // This is identical logic in ConcurrentDictionary.GetOrAdd method.
                var newItem = this.itemPolicy.CreateItem(key, valueFactory(key));

                if (this.dictionary.TryAdd(key, newItem))
                {
                    this.hotQueue.Enqueue(newItem);
                    Interlocked.Increment(ref hotCount);
                    Cycle();
                    return newItem.Value;
                }

                Disposer<V>.Dispose(newItem.Value);
            }
        }

        ///<inheritdoc/>
        public async Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out var value))
                {
                    return value;
                }

                // The value factory may be called concurrently for the same key, but the first write to the dictionary wins.
                // This is identical logic in ConcurrentDictionary.GetOrAdd method.
                var newItem = this.itemPolicy.CreateItem(key, await valueFactory(key).ConfigureAwait(false));

                if (this.dictionary.TryAdd(key, newItem))
                {
                    this.hotQueue.Enqueue(newItem);
                    Interlocked.Increment(ref hotCount);
                    Cycle();
                    return newItem.Value;
                }

                Disposer<V>.Dispose(newItem.Value);
            }
        }

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            while (true)
            { 
                if (this.dictionary.TryGetValue(key, out var existing))
                {
                    var kvp = new KeyValuePair<K, I>(key, existing);

                    // hidden atomic remove
                    // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
                    if (((ICollection<KeyValuePair<K, I>>)this.dictionary).Remove(kvp))
                    {
                        // Mark as not accessed, it will later be cycled out of the queues because it can never be fetched 
                        // from the dictionary. Note: Hot/Warm/Cold count will reflect the removed item until it is cycled 
                        // from the queue.
                        existing.WasAccessed = false;
                        existing.WasRemoved = true;

                        this.telemetryPolicy.OnItemRemoved(existing.Key, existing.Value, ItemRemovedReason.Removed);

                        // serialize dispose (common case dispose not thread safe)
                        lock (existing)
                        {
                            Disposer<V>.Dispose(existing.Value);
                        }

                        return true;
                    }

                    // it existed, but we couldn't remove - this means value was replaced afer the TryGetValue (a race), try again
                }
                else
                { 
                    return false;
                }
            }
        }

        ///<inheritdoc/>
        ///<remarks>Note: Calling this method does not affect LRU order.</remarks>
        public bool TryUpdate(K key, V value)
        {
            if (this.dictionary.TryGetValue(key, out var existing))
            {
                lock (existing)
                {
                    if (!existing.WasRemoved)
                    {
                        V oldValue = existing.Value;
                        existing.Value = value;

                        Disposer<V>.Dispose(oldValue);

                        return true;
                    }
                }
            }

            return false;
        }

        ///<inheritdoc/>
        ///<remarks>Note: Updates to existing items do not affect LRU order. Added items are at the top of the LRU.</remarks>
        public void AddOrUpdate(K key, V value)
        { 
            while (true)
            { 
                // first, try to update
                if (this.TryUpdate(key, value))
                { 
                    return;
                }

                // then try add
                var newItem = this.itemPolicy.CreateItem(key, value);

                if (this.dictionary.TryAdd(key, newItem))
                {
                    this.hotQueue.Enqueue(newItem);
                    Interlocked.Increment(ref hotCount);
                    Cycle();
                    return;
                }

                // if both update and add failed there was a race, try again
            }
        }

        ///<inheritdoc/>
        public void Clear()
        {
            int count= this.Count();

            for (int i = 0; i < count; i++)
            {
                CycleHotUnchecked(ItemRemovedReason.Cleared);
                CycleWarmUnchecked(ItemRemovedReason.Cleared);
                TryRemoveCold(ItemRemovedReason.Cleared);
            }
        }

        /// <summary>
        /// Trim the specified number of items from the cache. Removes all discardable items per IItemPolicy.ShouldDiscard(), then 
        /// itemCount-discarded items in LRU order, if any.
        /// </summary>
        /// <param name="itemCount">The number of items to remove.</param>
        /// <returns>The number of items removed from the cache.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemCount"/> is less than 0./</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemCount"/> is greater than capacity./</exception>
        /// <remarks>
        /// Note: Trim affects LRU order. Calling Trim resets the internal accessed status of items.
        /// </remarks>
        public void Trim(int itemCount)
        {
            int capacity = this.coldCapacity + this.warmCapacity + this.hotCapacity;

            if (itemCount < 1 || itemCount > capacity)
            { 
                throw new ArgumentOutOfRangeException(nameof(itemCount), "itemCount must be greater than or equal to one, and less than the capacity of the cache.");
            }

            // clamp itemCount to number of items actually in the cache
            itemCount = Math.Min(itemCount, this.HotCount + this.WarmCount + this.ColdCount);

            // first scan each queue for discardable items and remove them immediately. Note this can remove > itemCount items.
            int itemsRemoved = this.itemPolicy.CanDiscard() ? TrimAllDiscardedItems() : 0;

            TrimLiveItems(itemsRemoved, itemCount, capacity);
        }

        protected int TrimAllDiscardedItems()
        {
            int itemsRemoved = 0;

            void RemoveDiscardableItems(ConcurrentQueue<I> q, ref int queueCounter)
            {
                int localCount = queueCounter;

                for (int i = 0; i < localCount; i++)
                {
                    if (q.TryDequeue(out var item))
                    {
                        if (this.itemPolicy.ShouldDiscard(item))
                        {
                            Interlocked.Decrement(ref queueCounter);
                            this.Move(item, ItemDestination.Remove, ItemRemovedReason.Trimmed);
                            itemsRemoved++;
                        }
                        else
                        {
                            q.Enqueue(item);
                        }
                    }
                }
            }

            RemoveDiscardableItems(coldQueue, ref this.coldCount);
            RemoveDiscardableItems(warmQueue, ref this.warmCount);
            RemoveDiscardableItems(hotQueue, ref this.hotCount);

            return itemsRemoved;
        }

        private void TrimLiveItems(int itemsRemoved, int itemCount, int capacity)
        {
            // If clear is called during trimming, it would be possible to get stuck in an infinite
            // loop here. Instead quit after n consecutive failed attempts to move warm/hot to cold.
            int trimWarmAttempts = 0;
            int maxAttempts = this.coldCapacity + 1;

            while (itemsRemoved < itemCount && trimWarmAttempts < maxAttempts)
            {
                if (this.coldCount > 0)
                {
                    if (TryRemoveCold(ItemRemovedReason.Trimmed))
                    {
                        itemsRemoved++;
                        trimWarmAttempts = 0;
                    }

                    TrimWarmOrHot();
                }
                else
                {
                    TrimWarmOrHot();
                    trimWarmAttempts++;
                }
            }
        }

        private void TrimWarmOrHot()
        {
            if (this.warmCount > 0)
            {
                CycleWarmUnchecked(ItemRemovedReason.Trimmed);
            }
            else if (this.hotCount > 0)
            {
                CycleHotUnchecked(ItemRemovedReason.Trimmed);
            }
        }

        private void Cycle()
        {
            // There will be races when queue count == queue capacity. Two threads may each dequeue items.
            // This will prematurely free slots for the next caller. Each thread will still only cycle at most 5 items.
            // Since TryDequeue is thread safe, only 1 thread can dequeue each item. Thus counts and queue state will always
            // converge on correct over time.
            CycleHot();

            // Multi-threaded stress tests show that due to races, the warm and cold count can increase beyond capacity when
            // hit rate is very high. Double cycle results in stable count under all conditions. When contention is low, 
            // secondary cycles have no effect.
            CycleWarm();
            CycleWarm();
            CycleCold();
            CycleCold();
        }

        private void CycleHot()
        {
            if (this.hotCount > this.hotCapacity)
            {
                CycleHotUnchecked(ItemRemovedReason.Evicted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CycleHotUnchecked(ItemRemovedReason removedReason)
        {
            Interlocked.Decrement(ref this.hotCount);

            if (this.hotQueue.TryDequeue(out var item))
            {
                var where = this.itemPolicy.RouteHot(item);
                this.Move(item, where, removedReason);
            }
            else
            {
                Interlocked.Increment(ref this.hotCount);
            }
        }

        private void CycleWarm()
        {
            if (this.warmCount > this.warmCapacity)
            {
                CycleWarmUnchecked(ItemRemovedReason.Evicted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CycleWarmUnchecked(ItemRemovedReason removedReason)
        {
            Interlocked.Decrement(ref this.warmCount);

            if (this.warmQueue.TryDequeue(out var item))
            {
                var where = this.itemPolicy.RouteWarm(item);

                // When the warm queue is full, we allow an overflow of 1 item before redirecting warm items to cold.
                // This only happens when hit rate is high, in which case we can consider all items relatively equal in
                // terms of which was least recently used.
                if (where == ItemDestination.Warm && this.warmCount <= this.warmCapacity)
                {
                    this.Move(item, where, removedReason);
                }
                else
                {
                    this.Move(item, ItemDestination.Cold, removedReason);
                }
            }
            else
            {
                Interlocked.Increment(ref this.warmCount);
            }
        }

        private void CycleCold()
        {
            if (this.coldCount > this.coldCapacity)
            {
                TryRemoveCold(ItemRemovedReason.Evicted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryRemoveCold(ItemRemovedReason removedReason)
        {
            Interlocked.Decrement(ref this.coldCount);

            if (this.coldQueue.TryDequeue(out var item))
            {
                var where = this.itemPolicy.RouteCold(item);

                if (where == ItemDestination.Warm && this.warmCount <= this.warmCapacity)
                {
                    this.Move(item, where, removedReason);
                    return false;
                }
                else
                {
                    this.Move(item, ItemDestination.Remove, removedReason);
                    return true;
                }
            }
            else
            {
                Interlocked.Increment(ref this.coldCount);
                return false;
            }            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Move(I item, ItemDestination where, ItemRemovedReason removedReason)
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

                    var kvp = new KeyValuePair<K, I>(item.Key, item);

                    // hidden atomic remove
                    // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
                    if (((ICollection<KeyValuePair<K, I>>)this.dictionary).Remove(kvp))
                    {
                        item.WasRemoved = true;

                        this.telemetryPolicy.OnItemRemoved(item.Key, item.Value, removedReason);

                        lock (item)
                        {
                            Disposer<V>.Dispose(item.Value);
                        }
                    }
                    break;
            }
        }

        private static (int hot, int warm, int cold) ComputeQueueCapacity(int capacity)
        {
            int hotCapacity = capacity / 3;
            int warmCapacity = capacity / 3;
            int coldCapacity = capacity / 3;

            int remainder = capacity % 3;

            switch (remainder)
            {
                case 1:
                    coldCapacity++;
                    break;
                case 2:
                    hotCapacity++;
                    coldCapacity++;
                    break;
            }

            return (hotCapacity, warmCapacity, coldCapacity);
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
            return ((TemplateConcurrentLru<K, V, I, P, T>)this).GetEnumerator();
        }
    }
}
