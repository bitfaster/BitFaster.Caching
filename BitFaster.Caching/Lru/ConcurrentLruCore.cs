using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public class ConcurrentLruCore<K, V, I, P, T> : ICache<K, V>, IAsyncCache<K, V>, IEnumerable<KeyValuePair<K, V>>
        where I : LruItem<K, V>
        where P : struct, IItemPolicy<K, V, I>
        where T : struct, ITelemetryPolicy<K, V>
    {
        private readonly ConcurrentDictionary<K, I> dictionary;

        private readonly ConcurrentQueue<I> hotQueue;
        private readonly ConcurrentQueue<I> warmQueue;
        private readonly ConcurrentQueue<I> coldQueue;

        // maintain count outside ConcurrentQueue, since ConcurrentQueue.Count holds a global lock
        private PaddedQueueCount counter;

        private readonly ICapacityPartition capacity;

        private readonly P itemPolicy;
        private bool isWarm = false;

        // Since T is a struct, making it readonly will force the runtime to make defensive copies
        // if mutate methods are called. Therefore, field must be mutable to maintain count.
        protected T telemetryPolicy;

        public ConcurrentLruCore(
            int concurrencyLevel,
            ICapacityPartition capacity,
            IEqualityComparer<K> comparer,
            P itemPolicy,
            T telemetryPolicy)
        {
            if (capacity == null)
            {
                throw new ArgumentNullException(nameof(capacity));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            capacity.Validate();
            this.capacity = capacity;

            this.hotQueue = new ConcurrentQueue<I>();
            this.warmQueue = new ConcurrentQueue<I>();
            this.coldQueue = new ConcurrentQueue<I>();

            int dictionaryCapacity = this.Capacity + 1;

            this.dictionary = new ConcurrentDictionary<K, I>(concurrencyLevel, dictionaryCapacity, comparer);
            this.itemPolicy = itemPolicy;
            this.telemetryPolicy = telemetryPolicy;
            this.telemetryPolicy.SetEventSource(this);
        }

        // No lock count: https://arbel.net/2013/02/03/best-practices-for-using-concurrentdictionary/
        ///<inheritdoc/>
        public int Count => this.dictionary.Skip(0).Count();

        ///<inheritdoc/>
        public int Capacity => this.capacity.Hot + this.capacity.Warm + this.capacity.Cold;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => new Optional<ICacheMetrics>(new Proxy(this));

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => new Optional<ICacheEvents<K, V>>(new Proxy(this));

        public CachePolicy Policy => CreatePolicy(this);

        public int HotCount => this.counter.hot;

        public int WarmCount => this.counter.warm;

        public int ColdCount => this.counter.cold;

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
                    Interlocked.Increment(ref counter.hot);
                    Cycle();
                    return newItem.Value;
                }

                Disposer<V>.Dispose(newItem.Value);
            }
        }

        ///<inheritdoc/>
        public async ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
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
                    Interlocked.Increment(ref counter.hot);
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
                        this.itemPolicy.Update(existing);
                        this.telemetryPolicy.OnItemUpdated(existing.Key, existing.Value);
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
                    Interlocked.Increment(ref counter.hot);
                    Cycle();
                    return;
                }

                // if both update and add failed there was a race, try again
            }
        }

        ///<inheritdoc/>
        public void Clear()
        {
            int count = this.Count();

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
            int capacity = this.Capacity;

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

        private void TrimExpired()
        {
            if (this.itemPolicy.CanDiscard())
            {
                this.TrimAllDiscardedItems();
            }
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

            RemoveDiscardableItems(coldQueue, ref this.counter.cold);
            RemoveDiscardableItems(warmQueue, ref this.counter.warm);
            RemoveDiscardableItems(hotQueue, ref this.counter.hot);

            return itemsRemoved;
        }

        private void TrimLiveItems(int itemsRemoved, int itemCount, int capacity)
        {
            // If clear is called during trimming, it would be possible to get stuck in an infinite
            // loop here. Instead quit after n consecutive failed attempts to move warm/hot to cold.
            int trimWarmAttempts = 0;
            int maxAttempts = this.capacity.Cold + 1;

            while (itemsRemoved < itemCount && trimWarmAttempts < maxAttempts)
            {
                if (this.counter.cold > 0)
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
            if (this.counter.warm > 0)
            {
                CycleWarmUnchecked(ItemRemovedReason.Trimmed);
            }
            else if (this.counter.hot > 0)
            {
                CycleHotUnchecked(ItemRemovedReason.Trimmed);
            }
        }

        private void Cycle()
        {
            if (isWarm)
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
            else
            {
                // fill up the warm queue with new items until warm is full.
                // else during warmup the cache will only use the hot + cold queues until any item is requested twice.
                CycleDuringWarmup();
            }
        }

        private void CycleDuringWarmup()
        {
            // do nothing until hot is full
            if (this.counter.hot > this.capacity.Hot)
            {
                Interlocked.Decrement(ref this.counter.hot);

                if (this.hotQueue.TryDequeue(out var item))
                {
                    // always move to warm until it is full
                    if (this.counter.warm < this.capacity.Warm)
                    {
                        // If there is a race, we will potentially add multiple items to warm. Guard by cycling the queue.
                        this.Move(item, ItemDestination.Warm, ItemRemovedReason.Evicted);
                        CycleWarm();
                    }
                    else
                    {
                        // Else mark isWarm and move items to cold.
                        // If there is a race, we will potentially add multiple items to cold. Guard by cycling the queue.
                        Volatile.Write(ref this.isWarm, true);
                        this.Move(item, ItemDestination.Cold, ItemRemovedReason.Evicted);
                        CycleCold();
                    }
                }
                else
                {
                    Interlocked.Increment(ref this.counter.hot);
                }
            }
        }

        private void CycleHot()
        {
            if (this.counter.hot > this.capacity.Hot)
            {
                CycleHotUnchecked(ItemRemovedReason.Evicted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CycleHotUnchecked(ItemRemovedReason removedReason)
        {
            Interlocked.Decrement(ref this.counter.hot);

            if (this.hotQueue.TryDequeue(out var item))
            {
                var where = this.itemPolicy.RouteHot(item);
                this.Move(item, where, removedReason);
            }
            else
            {
                Interlocked.Increment(ref this.counter.hot);
            }
        }

        private void CycleWarm()
        {
            if (this.counter.warm > this.capacity.Warm)
            {
                CycleWarmUnchecked(ItemRemovedReason.Evicted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CycleWarmUnchecked(ItemRemovedReason removedReason)
        {
            Interlocked.Decrement(ref this.counter.warm);

            if (this.warmQueue.TryDequeue(out var item))
            {
                var where = this.itemPolicy.RouteWarm(item);

                // When the warm queue is full, we allow an overflow of 1 item before redirecting warm items to cold.
                // This only happens when hit rate is high, in which case we can consider all items relatively equal in
                // terms of which was least recently used.
                if (where == ItemDestination.Warm && this.counter.warm <= this.capacity.Warm)
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
                Interlocked.Increment(ref this.counter.warm);
            }
        }

        private void CycleCold()
        {
            if (this.counter.cold > this.capacity.Cold)
            {
                TryRemoveCold(ItemRemovedReason.Evicted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryRemoveCold(ItemRemovedReason removedReason)
        {
            Interlocked.Decrement(ref this.counter.cold);

            if (this.coldQueue.TryDequeue(out var item))
            {
                var where = this.itemPolicy.RouteCold(item);

                if (where == ItemDestination.Warm && this.counter.warm <= this.capacity.Warm)
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
                Interlocked.Increment(ref this.counter.cold);
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
                    Interlocked.Increment(ref this.counter.warm);
                    break;
                case ItemDestination.Cold:
                    this.coldQueue.Enqueue(item);
                    Interlocked.Increment(ref this.counter.cold);
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
            return ((ConcurrentLruCore<K, V, I, P, T>)this).GetEnumerator();
        }

        private static CachePolicy CreatePolicy(ConcurrentLruCore<K, V, I, P, T> lru)
        { 
            var p = new Proxy(lru); 
            return new CachePolicy(new Optional<IBoundedPolicy>(p), lru.itemPolicy.CanDiscard() ? new Optional<ITimePolicy>(p) : Optional<ITimePolicy>.None()); 
        }

        // To get JIT optimizations, policies must be structs.
        // If the structs are returned directly via properties, they will be copied. Since  
        // telemetryPolicy is a mutable struct, copy is bad. One workaround is to store the 
        // state within the struct in an object. Since the struct points to the same object
        // it becomes immutable. However, this object is then somewhere else on the 
        // heap, which slows down the policies with hit counter logic in benchmarks. Likely
        // this approach keeps the structs data members in the same CPU cache line as the LRU.
        private class Proxy : ICacheMetrics, ICacheEvents<K, V>, IBoundedPolicy, ITimePolicy
        {
            private readonly ConcurrentLruCore<K, V, I, P, T> lru;

            public Proxy(ConcurrentLruCore<K, V, I, P, T> lru)
            {
                this.lru = lru;
            }

            public double HitRatio => lru.telemetryPolicy.HitRatio;

            public long Total => lru.telemetryPolicy.Total;

            public long Hits => lru.telemetryPolicy.Hits;

            public long Misses => lru.telemetryPolicy.Misses;

            public long Evicted => lru.telemetryPolicy.Evicted;

            public long Updated => lru.telemetryPolicy.Updated;

            public int Capacity => lru.Capacity;

            public TimeSpan TimeToLive => lru.itemPolicy.TimeToLive;

            public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
            {
                add { this.lru.telemetryPolicy.ItemRemoved += value; }
                remove { this.lru.telemetryPolicy.ItemRemoved -= value; }
            }

            public void Trim(int itemCount)
            {
                lru.Trim(itemCount);
            }

            public void TrimExpired()
            {
                lru.TrimExpired();
            }
        }
    }
}
