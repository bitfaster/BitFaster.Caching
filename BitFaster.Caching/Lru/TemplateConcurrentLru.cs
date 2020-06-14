using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    public class TemplateConcurrentLru<K, V, I, P, H> : ICache<K, V>
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
        protected H hitCounter;

        public TemplateConcurrentLru(
            int concurrencyLevel,
            int capacity,
            IEqualityComparer<K> comparer,
            P itemPolicy,
            H hitCounter)
        {
            if (capacity < 3)
            {
                throw new ArgumentOutOfRangeException("Capacity must be greater than or equal to 3.");
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }    

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

        public int Count => this.dictionary.Count;

        public int HotCount => this.hotCount;

        public int WarmCount => this.warmCount;

        public int ColdCount => this.coldCount;

        public bool TryGet(K key, out V value)
        {
            I item;
            if (dictionary.TryGetValue(key, out item))
            {
                if (this.policy.ShouldDiscard(item))
                {
                    this.Move(item, ItemDestination.Remove);
                    value = default(V);
                    return false;
                }

                value = item.Value;
                this.policy.Touch(item);
                this.hitCounter.IncrementHit();
                return true;
            }

            value = default(V);
            this.hitCounter.IncrementMiss();
            return false;
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            if (this.TryGet(key, out var value))
            {
                return value;
            }

            // The value factory may be called concurrently for the same key, but the first write to the dictionary wins.
            // This is identical logic in ConcurrentDictionary.GetOrAdd method.
            var newItem = this.policy.CreateItem(key, valueFactory(key));

            if (this.dictionary.TryAdd(key, newItem))
            {
                this.hotQueue.Enqueue(newItem);
                Interlocked.Increment(ref hotCount);
                Cycle();
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
            // This is identical logic in ConcurrentDictionary.GetOrAdd method.
            var newItem = this.policy.CreateItem(key, await valueFactory(key).ConfigureAwait(false));

            if (this.dictionary.TryAdd(key, newItem))
            {
                this.hotQueue.Enqueue(newItem);
                Interlocked.Increment(ref hotCount);
                Cycle();
                return newItem.Value;
            }

            return await this.GetOrAddAsync(key, valueFactory).ConfigureAwait(false);
        }

        public bool TryRemove(K key)
        {
            // Possible race condition:
            // Thread A TryRemove(1), removes LruItem1, has reference to removed item but not yet marked as removed
            // Thread B GetOrAdd(1) => Adds LruItem1*
            // Thread C GetOrAdd(2), Cycle, Move(LruItem1, Removed)
            // 
            // Thread C can run and remove LruItem1* from this.dictionary before Thread A has marked LruItem1 as removed.
            // 
            // In this situation, a subsequent attempt to fetch 1 will be a miss. The queues will still contain LruItem1*, 
            // and it will not be marked as removed. If key 1 is fetched while LruItem1* is still in the queue, there will 
            // be two queue entries for key 1, and neither is marked as removed. Thus when LruItem1 * ages out, it will  
            // incorrectly remove 1 from the dictionary, and this cycle can repeat.
            if (this.dictionary.TryGetValue(key, out var existing))
            {
                if (existing.WasRemoved)
                {
                    return false;
                }

                lock (existing)
                {
                    if (existing.WasRemoved)
                    {
                        return false;
                    }

                    existing.WasRemoved = true;
                }

                if (this.dictionary.TryRemove(key, out var removedItem))
                {
                    // Mark as not accessed, it will later be cycled out of the queues because it can never be fetched 
                    // from the dictionary. Note: Hot/Warm/Cold count will reflect the removed item until it is cycled 
                    // from the queue.
                    removedItem.WasAccessed = false;

                    if (removedItem.Value is IDisposable d)
                    {
                        d.Dispose();
                    }

                    return true;
                }
            }

            return false;
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

        private void CycleWarm()
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

        private void CycleCold()
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
                        this.Move(item, ItemDestination.Remove);
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
                    if (!item.WasRemoved)
                    {   
                        // avoid race where 2 threads could remove the same key - see TryRemove for details.
                        lock (item)
                        { 
                            if (item.WasRemoved)
                            {
                                break;
                            }

                            if (this.dictionary.TryRemove(item.Key, out var removedItem))
                            {
                                item.WasRemoved = true;
                                if (removedItem.Value is IDisposable d)
                                {
                                    d.Dispose();
                                }
                            }
                        }
                    }
                    break;
            }
        }
    }
}
