using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using BitFaster.Caching.Counters;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

#if DEBUG
using System.Linq;
using System.Text;
#endif

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// An approximate LFU based on the W-TinyLfu eviction policy. W-TinyLfu tracks items using a window LRU list, and 
    /// a main space LRU divided into protected and probation segments. Reads and writes to the cache are stored in buffers
    /// and later applied to the policy LRU lists in batches under a lock. Each read and write is tracked using a compact 
    /// popularity sketch to probalistically estimate item frequency. Items proceed through the LRU lists as follows:
    /// <list type="number">
    ///   <item><description>New items are added to the window LRU. When acessed window items move to the window MRU position.</description></item>
    ///   <item><description>When the window is full, candidate items are moved to the probation segment in LRU order.</description></item>
    ///   <item><description>When the main space is full, the access frequency of each window candidate is compared 
    ///   to probation victims in LRU order. The item with the lowest frequency is evicted until the cache size is within bounds.</description></item>
    ///   <item><description>When a probation item is accessed, it is moved to the protected segment. If the protected segment is full, 
    ///   the LRU protected item is demoted to probation.</description></item>
    ///   <item><description>When a protected item is accessed, it is moved to the protected MRU position.</description></item>
    /// </list>
    /// The size of the admission window and main space are adapted over time to iteratively improve hit rate using a 
    /// hill climbing algorithm. A larger window favors workloads with high recency bias, whereas a larger main space
    /// favors workloads with frequency bias.
    /// </summary>
    /// Based on the Caffeine library by ben.manes@gmail.com (Ben Manes).
    /// https://github.com/ben-manes/caffeine
    [DebuggerTypeProxy(typeof(ConcurrentLfu<,>.LfuDebugView))]
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public sealed class ConcurrentLfu<K, V> : ICache<K, V>, IAsyncCache<K, V>, IBoundedPolicy
    {
        private const int MaxWriteBufferRetries = 64;

        /// <summary>
        /// The default buffer size.
        /// </summary>
        public const int DefaultBufferSize = 128;

        private readonly ConcurrentDictionary<K, LfuNode<K, V>> dictionary;

        private readonly StripedMpscBuffer<LfuNode<K, V>> readBuffer;
        private readonly MpscBoundedBuffer<LfuNode<K, V>> writeBuffer;

        private readonly CacheMetrics metrics = new();

        private readonly CmSketch<K> cmSketch;

        private readonly LfuNodeList<K, V> windowLru;
        private readonly LfuNodeList<K, V> probationLru;
        private readonly LfuNodeList<K, V> protectedLru;

        private readonly LfuCapacityPartition capacity;

        private readonly DrainStatus drainStatus = new();
        private readonly object maintenanceLock = new();

        private readonly IScheduler scheduler;

        private readonly LfuNode<K, V>[] drainBuffer;

        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified capacity.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public ConcurrentLfu(int capacity)
            : this(Defaults.ConcurrencyLevel, capacity, new ThreadPoolScheduler(), EqualityComparer<K>.Default)
        {        
        }

        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified concurrencyLevel, capacity, scheduler, equality comparer and buffer size.
        /// </summary>
        /// <param name="concurrencyLevel">The concurrency level.</param>
        /// <param name="capacity">The capacity.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <param name="comparer">The equality comparer.</param>
        public ConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer)
        {
            this.dictionary = new ConcurrentDictionary<K, LfuNode<K, V>>(concurrencyLevel, capacity, comparer);

            // cap concurrency at proc count * 2
            int readStripes = Math.Min(BitOps.CeilingPowerOfTwo(concurrencyLevel), BitOps.CeilingPowerOfTwo(Environment.ProcessorCount * 2));
            this.readBuffer = new StripedMpscBuffer<LfuNode<K, V>>(readStripes, DefaultBufferSize);

            // Cap the write buffer to the cache size, or 128. Whichever is smaller.
            int writeBufferSize = Math.Min(BitOps.CeilingPowerOfTwo(capacity), 128);
            this.writeBuffer = new MpscBoundedBuffer<LfuNode<K, V>>(writeBufferSize);

            this.cmSketch = new CmSketch<K>(capacity, comparer);
            this.windowLru = new LfuNodeList<K, V>();
            this.probationLru = new LfuNodeList<K, V>();
            this.protectedLru = new LfuNodeList<K, V>();

            this.capacity = new LfuCapacityPartition(capacity);

            this.scheduler = scheduler;

            this.drainBuffer = new LfuNode<K, V>[this.readBuffer.Capacity];
        }

        ///<inheritdoc/>
        public int Count => this.dictionary.Count;

        ///<inheritdoc/>
        public int Capacity => this.capacity.Capacity;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => new(this.metrics);

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => Optional<ICacheEvents<K, V>>.None();

        ///<inheritdoc/>
        public CachePolicy Policy => new(new Optional<IBoundedPolicy>(this), Optional<ITimePolicy>.None());

        ///<inheritdoc/>
        public ICollection<K> Keys => this.dictionary.Keys;

        /// <summary>
        /// Gets the scheduler.
        /// </summary>
        public IScheduler Scheduler => scheduler;

        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            while (true)
            {
                if (TryUpdate(key, value))
                {
                    return;
                }

                var node = new LfuNode<K, V>(key, value);
                if (this.dictionary.TryAdd(key, node))
                {
                    AfterWrite(node);
                    return;
                }
            }
        }

        ///<inheritdoc/>
        public void Clear()
        {
            this.Trim(this.Count);

            lock (maintenanceLock)
            {
                this.cmSketch.Clear();
                this.readBuffer.Clear();
                this.writeBuffer.Clear();
            }
        }

        /// <summary>
        /// Trim the specified number of items from the cache.
        /// </summary>
        /// <param name="itemCount">The number of items to remove.</param>
        public void Trim(int itemCount)
        {
            itemCount = Math.Min(itemCount, this.Count);
            var candidates = new List<LfuNode<K, V>>(itemCount);

            // TODO: this is LRU order eviction, Caffeine is based on frequency
            lock (maintenanceLock)
            {
                // flush all buffers
                Maintenance();

                // walk in lru order, get itemCount keys to evict
                TakeCandidatesInLruOrder(this.probationLru, candidates, itemCount);
                TakeCandidatesInLruOrder(this.protectedLru, candidates, itemCount);
                TakeCandidatesInLruOrder(this.windowLru, candidates, itemCount);
            }

            foreach (var candidate in candidates)
            {
                this.TryRemove(candidate.Key);
            }
        }

        private bool TryAdd(K key, V value)
        {
            var node = new LfuNode<K, V>(key, value);

            if (this.dictionary.TryAdd(key, node))
            {
                AfterWrite(node);
                return true;
            }

            Disposer<V>.Dispose(node.Value);
            return false;
        }

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out V value))
                {
                    return value;
                }

                value = valueFactory(key);
                if (this.TryAdd(key, value))
                {
                    return value;
                }
            }
        }

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already 
        /// in the cache, or the new value if the key was not in the cache.</returns>
        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            while (true)
            {
                if (this.TryGet(key, out V value))
                {
                    return value;
                }

                value = valueFactory(key, factoryArgument);
                if (this.TryAdd(key, value))
                {
                    return value;
                }
            }
        }

        ///<inheritdoc/>
        public async ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out V value))
                {
                    return value;
                }

                value = await valueFactory(key).ConfigureAwait(false);
                if (this.TryAdd(key, value))
                {
                    return value;
                }
            }
        }

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        public async ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            while (true)
            {
                if (this.TryGet(key, out V value))
                {
                    return value;
                }

                value = await valueFactory(key, factoryArgument).ConfigureAwait(false);
                if (this.TryAdd(key, value))
                {
                    return value;
                }
            }
        }

        ///<inheritdoc/>
        public bool TryGet(K key, out V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                bool delayable = this.readBuffer.TryAdd(node) != BufferStatus.Full;

                if (this.drainStatus.ShouldDrain(delayable))
                { 
                    TryScheduleDrain(); 
                }
                value = node.Value;               
                return true;
            }

            this.metrics.requestMissCount.Increment();

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to remove the specified key value pair.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>true if the item was removed successfully; otherwise, false.</returns>
        public bool TryRemove(KeyValuePair<K, V> item)
        {
            if (this.dictionary.TryGetValue(item.Key, out var node))
            {
                if (EqualityComparer<V>.Default.Equals(node.Value, item.Value))
                {
                    var kvp = new KeyValuePair<K, LfuNode<K,V>>(item.Key, node);

#if NET6_0_OR_GREATER
                    if (this.dictionary.TryRemove(kvp))
#else
                    // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
                    if (((ICollection<KeyValuePair<K, LfuNode<K, V>>>)this.dictionary).Remove(kvp))
#endif
                    {
                        node.WasRemoved = true;
                        AfterWrite(node);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to remove and return the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed, or the default value of the value type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public bool TryRemove(K key, out V value)
        {
            if (this.dictionary.TryRemove(key, out var node))
            {
                node.WasRemoved = true;
                AfterWrite(node);
                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            return this.TryRemove(key, out var _);
        }

        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                node.Value = value;

                // It's ok for this to be lossy, since the node is already tracked
                // and we will just lose ordering/hit count, but not orphan the node.
                this.writeBuffer.TryAdd(node);
                TryScheduleDrain();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Synchronously perform all pending policy maintenance. Drain the read and write buffers then
        /// use the eviction policy to preserve bounded size and remove expired items.
        /// </summary>
        /// <remarks>
        /// Note: maintenance is automatically performed asynchronously immediately following a read or write.
        /// It is not necessary to call this method, <see cref="DoMaintenance"/> is provided purely to enable tests to reach a consistent state.
        /// </remarks>
        public void DoMaintenance()
        {
            DrainBuffers();
        }

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

        private static void TakeCandidatesInLruOrder(LfuNodeList<K, V> lru, List<LfuNode<K, V>> candidates, int itemCount)
        {
            var curr = lru.First;

            while (candidates.Count < itemCount && curr != null)
            {
                // LRUs can contain items that are already removed, skip those 
                if (!curr.WasRemoved)
                { 
                    candidates.Add(curr); 
                }

                curr = curr.Next;
            }
        }

        private void AfterWrite(LfuNode<K, V> node)
        {
            for (int i = 0; i < MaxWriteBufferRetries; i++)
            {
                if (writeBuffer.TryAdd(node) == BufferStatus.Success)
                {
                    ScheduleAfterWrite();
                    return;
                }

                TryScheduleDrain();
            }

            lock (this.maintenanceLock)
            {
                // aggressively try to exit the lock early before doing full maintenance
                var status = BufferStatus.Contended;
                while (status != BufferStatus.Full)
                {
                    status = writeBuffer.TryAdd(node);

                    if (status == BufferStatus.Success)
                    {
                        ScheduleAfterWrite();
                        return;
                    }
                }

                // if the write was dropped from the buffer, explicitly pass it to maintenance
                Maintenance(node);
            }
        }

        private void ScheduleAfterWrite()
        {
            var spinner = new SpinWait();
            while (true)
            {
                switch (this.drainStatus.Status())
                {
                    case DrainStatus.Idle:
                        this.drainStatus.Cas(DrainStatus.Idle, DrainStatus.Required);
                        TryScheduleDrain();
                        return;
                    case DrainStatus.Required:
                        TryScheduleDrain();
                        return;
                    case DrainStatus.ProcessingToIdle:
                        if (this.drainStatus.Cas(DrainStatus.ProcessingToIdle, DrainStatus.ProcessingToRequired))
                        {
                            return;
                        }
                        break;
                    case DrainStatus.ProcessingToRequired:
                        return;
                }
                spinner.SpinOnce();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ConcurrentLfu<K, V>)this).GetEnumerator();
        }

        private void TryScheduleDrain()
        {
            if (this.drainStatus.Status() >= DrainStatus.ProcessingToIdle)
            {
                return;
            }

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(maintenanceLock, ref lockTaken);

                if (lockTaken)
                {
                    int status = this.drainStatus.Status();

                    if (status >= DrainStatus.ProcessingToIdle)
                    {
                        return;
                    }

                    this.drainStatus.Set(DrainStatus.ProcessingToIdle);
                    scheduler.Run(() => DrainBuffers());
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(maintenanceLock);
                }
            }
        }

        private void DrainBuffers()
        {
            bool done = false;

            while (!done)
            {
                lock (maintenanceLock)
                {
                    done = Maintenance();
                }

                // don't run continuous foreground maintenance
                if (!scheduler.IsBackground)
                {
                    done = true;
                }
            }

            if (this.drainStatus.Status() == DrainStatus.Required)
            {
                TryScheduleDrain();
            }
        }

        private bool Maintenance(LfuNode<K, V> droppedWrite = null)
        {
            this.drainStatus.Set(DrainStatus.ProcessingToIdle);

            // Note: this is only Span on .NET Core 3.1+, else this is no-op and it is still an array
            var buffer = this.drainBuffer.AsSpanOrArray();

            // extract to a buffer before doing book keeping work, ~2x faster
            int readCount = readBuffer.DrainTo(buffer);

            for (int i = 0; i < readCount; i++)
            {
                this.cmSketch.Increment(buffer[i].Key);
            }

            for (int i = 0; i < readCount; i++)
            {
                OnAccess(buffer[i]);
            }

            int writeCount = this.writeBuffer.DrainTo(buffer.AsSpanOrSegment());

            for (int i = 0; i < writeCount; i++)
            {
                OnWrite(buffer[i]);
            }

            // we are done only when both buffers are empty
            var done = readCount == 0 & writeCount == 0;

            if (droppedWrite != null)
            {
                OnWrite(droppedWrite);
                done = true;
            }

            EvictEntries();
            this.capacity.OptimizePartitioning(this.metrics, this.cmSketch.ResetSampleSize);
            ReFitProtected();

            // Reset to idle if either
            // 1. We drained both input buffers (all work done)
            // 2. or scheduler is foreground (since don't run continuously on the foreground)
            if ((done || !scheduler.IsBackground) &&
                (this.drainStatus.Status() != DrainStatus.ProcessingToIdle ||
                !this.drainStatus.Cas(DrainStatus.ProcessingToIdle, DrainStatus.Idle)))
            {
                this.drainStatus.Set(DrainStatus.Required);
            }

            return done;
        }

        private void OnAccess(LfuNode<K, V> node)
        {
            // there was a cache hit even if the item was removed or is not yet added.
            this.metrics.requestHitCount++;

            // Node is added to read buffer while it is removed by maintenance, or it is read before it has been added.
            if (node.list == null)
            {
                return;
            }

            switch (node.Position)
            {
                case Position.Window:
                    this.windowLru.MoveToEnd(node); 
                    break;
                case Position.Probation:
                    PromoteProbation(node); 
                    break;
                case Position.Protected:
                    this.protectedLru.MoveToEnd(node);
                    break;
            }
        }

        private void OnWrite(LfuNode<K, V> node)
        {
            // Nodes can be removed while they are in the write buffer, in which case they should
            // not be added back into the LRU.
            if (node.WasRemoved)
            {
                node.list?.Remove(node);

                if (!node.WasDeleted)
                {
                    // if a write is in the buffer and is then removed in the buffer, it will enter OnWrite twice.
                    // we mark as deleted to avoid double counting/disposing it
                    this.metrics.evictedCount++;
                    Disposer<V>.Dispose(node.Value);
                    node.WasDeleted = true;
                }

                return;
            }

            this.cmSketch.Increment(node.Key);

            // node can already be in one of the queues due to update
            switch (node.Position)
            {
                case Position.Window:
                    if (node.list == null)
                    {
                        this.windowLru.AddLast(node);
                    }
                    else
                    {
                        this.windowLru.MoveToEnd(node);
                        this.metrics.updatedCount++;
                    }
                    break;
                case Position.Probation:
                    PromoteProbation(node);
                    this.metrics.updatedCount++;
                    break;
                case Position.Protected:
                    this.protectedLru.MoveToEnd(node);
                    this.metrics.updatedCount++;
                    break;
            }
        }

        private void PromoteProbation(LfuNode<K, V> node)
        {
            this.probationLru.Remove(node);
            this.protectedLru.AddLast(node);
            node.Position = Position.Protected;

            // If the protected space exceeds its maximum, the LRU items are demoted to the probation space.
            if (this.protectedLru.Count > this.capacity.Protected)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();

                demoted.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        private void EvictEntries()
        {
            var candidate = EvictFromWindow();
            EvictFromMain(candidate);
        }

        private LfuNode<K, V> EvictFromWindow()
        {
            LfuNode<K, V> first = null;

            while (this.windowLru.Count > this.capacity.Window)
            {
                var node = this.windowLru.First;
                this.windowLru.RemoveFirst();

                first ??= node;

                this.probationLru.AddLast(node);
                node.Position = Position.Probation;
            }

            return first;
        }

        private ref struct EvictIterator
        {
            private readonly CmSketch<K> sketch;
            public LfuNode<K, V> node;
            public int freq;

            public EvictIterator(CmSketch<K> sketch, LfuNode<K, V> node)
            {
                this.sketch = sketch;
                this.node = node;
                freq = node == null ? -1 : sketch.EstimateFrequency(node.Key);
            }

            public void Next()
            {
                node = node.Next;

                if (node != null)
                {
                    freq = sketch.EstimateFrequency(node.Key);
                }
            }
        }

        private void EvictFromMain(LfuNode<K, V> candidateNode)
        {
            var victim = new EvictIterator(this.cmSketch, this.probationLru.First); // victims are LRU position in probation
            var candidate = new EvictIterator(this.cmSketch, candidateNode);

            // first pass: admit candidates
            while (this.windowLru.Count + this.probationLru.Count + this.protectedLru.Count > this.Capacity)
            {
                // bail when we run out of options
                if (candidate.node == null | victim.node == null)
                {
                    break;
                }

                if (victim.node == candidate.node)
                {
                    Evict(candidate.node);
                    break;
                }

                // Evict the entry with the lowest frequency
                if (candidate.freq > victim.freq)
                {
                    var evictee = victim.node;

                    // victim is initialized to first, and iterates forwards
                    victim.Next();
                    candidate.Next();

                    Evict(evictee);
                }
                else
                {
                    var evictee = candidate.node;

                    // candidate is initialized to first cand, and iterates forwards
                    candidate.Next();

                    Evict(evictee);
                }
            }

            // 2nd pass: remove probation items in LRU order, evict lowest frequency
            while (this.windowLru.Count + this.probationLru.Count + this.protectedLru.Count > this.Capacity)
            {
                var victim1 = this.probationLru.First;
                var victim2 = victim1.Next;

                if (AdmitCandidate(victim1.Key, victim2.Key))
                {
                    Evict(victim2);
                }
                else
                {
                    Evict(victim1);
                }
            }
        }

        private bool AdmitCandidate(K candidateKey, K victimKey)
        {
            int victimFreq = this.cmSketch.EstimateFrequency(victimKey);
            int candidateFreq = this.cmSketch.EstimateFrequency(candidateKey);

            //var (victimFreq, candidateFreq) = this.cmSketch.EstimateFrequency(victimKey, candidateKey);

            // TODO: random factor when candidate freq < 5
            return candidateFreq > victimFreq;
        }

        private void Evict(LfuNode<K, V> evictee)
        {
            this.dictionary.TryRemove(evictee.Key, out var _);
            evictee.list.Remove(evictee);
            Disposer<V>.Dispose(evictee.Value);
            this.metrics.evictedCount++;
        }

        private void ReFitProtected()
        {
            // If hill climbing decreased protected, there may be too many items
            // - demote overflow to probation.
            while (this.protectedLru.Count > this.capacity.Protected)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();

                demoted.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        [DebuggerDisplay("{Format(),nq}")]
        private class DrainStatus
        {
            public const int Idle = 0;
            public const int Required = 1;
            public const int ProcessingToIdle = 2;
            public const int ProcessingToRequired = 3;

            private PaddedInt drainStatus; // mutable struct, don't mark readonly

            public bool ShouldDrain(bool delayable)
            {
                int status = Volatile.Read(ref this.drainStatus.Value);
                return status switch
                {
                    Idle => !delayable,
                    Required => true,
                    ProcessingToIdle or ProcessingToRequired => false,
                    _ => false,// not reachable
                };
            }

            public void Set(int newStatus)
            { 
                Volatile.Write(ref this.drainStatus.Value, newStatus);
            }

            public bool Cas(int oldStatus, int newStatus)
            { 
                return Interlocked.CompareExchange(ref this.drainStatus.Value, newStatus, oldStatus) == oldStatus;
            }

            public int Status()
            {
                return Volatile.Read(ref this.drainStatus.Value);
            }

            [ExcludeFromCodeCoverage]
            internal string Format()
            {
                switch (this.drainStatus.Value)
                {
                    case Idle:
                        return "Idle";
                    case Required:
                        return "Required";
                    case ProcessingToIdle:
                        return "ProcessingToIdle";
                    case ProcessingToRequired:
                        return "ProcessingToRequired"; ;
                }

                return "Invalid state";
            }
        }

        [DebuggerDisplay("Hit = {Hits}, Miss = {Misses}, Upd = {Updated}, Evict = {Evicted}")]
        internal class CacheMetrics : ICacheMetrics
        {
            public long requestHitCount;
            public Counter requestMissCount = new();
            public long updatedCount;
            public long evictedCount;

            public double HitRatio => (double)requestHitCount / (double)Total;

            public long Total => requestHitCount + requestMissCount.Count();

            public long Hits => requestHitCount;

            public long Misses => requestMissCount.Count();

            public long Updated => updatedCount;

            public long Evicted => evictedCount;
        }

#if DEBUG
        /// <summary>
        /// Format the LFU as a string by converting all the keys to strings.
        /// </summary>
        /// <returns>The LFU formatted as a string.</returns>
        public string FormatLfuString()
        {
            var sb = new StringBuilder();

            sb.Append("W [");
            sb.Append(string.Join(",", this.windowLru.Select(n => n.Key.ToString())));
            sb.Append("] Protected [");
            sb.Append(string.Join(",", this.protectedLru.Select(n => n.Key.ToString())));
            sb.Append("] Probation [");
            sb.Append(string.Join(",", this.probationLru.Select(n => n.Key.ToString())));
            sb.Append("]");

            return sb.ToString();
        }
#endif

        [ExcludeFromCodeCoverage]
        internal class LfuDebugView
        {
            private readonly ConcurrentLfu<K, V> lfu;

            public LfuDebugView(ConcurrentLfu<K, V> lfu)
            {
                this.lfu = lfu;
            }

            public string Maintenance => lfu.drainStatus.Format();

            public ICacheMetrics Metrics => lfu.metrics;

            public StripedMpscBuffer<LfuNode<K, V>> ReadBuffer => this.lfu.readBuffer;

            public MpscBoundedBuffer<LfuNode<K, V>> WriteBuffer => this.lfu.writeBuffer;

            public KeyValuePair<K, V>[] Items
            {
                get
                {
                    var items = new KeyValuePair<K, V>[lfu.Count];

                    int index = 0;
                    foreach (var kvp in lfu)
                    {
                        items[index++] = kvp;
                    }
                    return items;
                }
            }
        }
    }

    // Explicit layout cannot be a generic class member
    [StructLayout(LayoutKind.Explicit, Size = 2 * Padding.CACHE_LINE_SIZE)]
    internal struct PaddedInt
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public int Value;
    }
}
