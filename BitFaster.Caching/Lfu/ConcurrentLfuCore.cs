using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using BitFaster.Caching.Counters;
using BitFaster.Caching.Scheduler;

#if DEBUG
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

    internal struct ConcurrentLfuCore<K, V, N, P> : IBoundedPolicy
        where K : notnull
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N>
    {
        private const int MaxWriteBufferRetries = 64;

        private const int DefaultBufferSize = 128;

        private readonly ConcurrentDictionary<K, N> dictionary;

        internal readonly StripedMpscBuffer<N> readBuffer;
        internal readonly MpscBoundedBuffer<N> writeBuffer;

        private readonly CacheMetrics metrics = new();

        private readonly CmSketch<K> cmSketch;

        private readonly LfuNodeList<K, V> windowLru;
        private readonly LfuNodeList<K, V> probationLru;
        private readonly LfuNodeList<K, V> protectedLru;

        private readonly LfuCapacityPartition capacity;

        internal readonly DrainStatus drainStatus = new();

        private readonly SecondaryBufferSet[] secondaryBuffers;

        private int secondaryBufferIndex;
        private int activeDrainCount;

#if NET9_0_OR_GREATER
        private readonly Lock maintenanceLock = new();
        private readonly Lock primaryReadBufferLock = new();
        private readonly Lock primaryWriteBufferLock = new();
#else
        private readonly object maintenanceLock = new();
        private readonly object primaryReadBufferLock = new();
        private readonly object primaryWriteBufferLock = new();
#endif

        private readonly IScheduler scheduler;
        private readonly Action drainBuffers;

        internal P policy;

        public ConcurrentLfuCore(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, Action drainBuffers, P policy)
        {
            if (capacity < 3)
                Throw.ArgOutOfRange(nameof(capacity));

            int dictionaryCapacity = ConcurrentDictionarySize.Estimate(capacity);
            this.dictionary = new(concurrencyLevel, dictionaryCapacity, comparer);

            // cap concurrency at proc count * 2
            int readStripes = Math.Min(BitOps.CeilingPowerOfTwo(concurrencyLevel), BitOps.CeilingPowerOfTwo(Environment.ProcessorCount * 2));
            this.readBuffer = new(readStripes, DefaultBufferSize);

            // Cap the write buffer to the cache size, or 128. Whichever is smaller.
            int writeBufferSize = Math.Min(BitOps.CeilingPowerOfTwo(capacity), 128);
            this.writeBuffer = new(writeBufferSize);

            this.cmSketch = new CmSketch<K>(capacity, comparer);
            this.windowLru = new LfuNodeList<K, V>();
            this.probationLru = new LfuNodeList<K, V>();
            this.protectedLru = new LfuNodeList<K, V>();

            this.capacity = new LfuCapacityPartition(capacity);

            this.scheduler = scheduler;

            this.secondaryBuffers =
            [
                new(this.readBuffer.Capacity, this.writeBuffer.Capacity),
                new(this.readBuffer.Capacity, this.writeBuffer.Capacity),
            ];

            this.drainBuffers = drainBuffers;

            this.policy = policy;
        }

        // No lock count: https://arbel.net/2013/02/03/best-practices-for-using-concurrentdictionary/
        public int Count => this.dictionary.Skip(0).Count();

        public int Capacity => this.capacity.Capacity;

        public Optional<ICacheMetrics> Metrics => new(this.metrics);

        public CachePolicy Policy => new(new Optional<IBoundedPolicy>(this), Optional<ITimePolicy>.None());

        public ICollection<K> Keys => this.dictionary.Keys;

#if NET9_0_OR_GREATER
        public IEqualityComparer<K> Comparer => this.dictionary.Comparer;
#endif

        public IScheduler Scheduler => scheduler;

        public void AddOrUpdate(K key, V value)
        {
            while (true)
            {
                if (TryUpdate(key, value))
                {
                    return;
                }

                var node = policy.Create(key, value);
                if (this.dictionary.TryAdd(key, node))
                {
                    AfterWrite(node);
                    return;
                }
            }
        }

        public void Clear()
        {
            Trim(int.MaxValue);

            lock (maintenanceLock)
            {
                lock (this.primaryReadBufferLock)
                {
                    this.readBuffer.Clear();
                }

                lock (this.primaryWriteBufferLock)
                {
                    this.writeBuffer.Clear();
                }

                ClearSecondaryBuffers();
                this.cmSketch.Clear();
                Volatile.Write(ref this.secondaryBufferIndex, 0);
                Volatile.Write(ref this.activeDrainCount, 0);
                this.drainStatus.NonVolatileWrite(DrainStatus.Idle);
            }
        }

        public void Trim(int itemCount)
        {
            DoMaintenance();

            List<LfuNode<K, V>> candidates;
            lock (maintenanceLock)
            {
                int lruCount = this.windowLru.Count + this.probationLru.Count + this.protectedLru.Count;
                itemCount = Math.Min(itemCount, lruCount);
                candidates = new(itemCount);

                // Note: this is LRU order eviction, Caffeine is based on frequency
                // walk in lru order, get itemCount keys to evict
                TakeCandidatesInLruOrder(this.probationLru, candidates, itemCount);
                TakeCandidatesInLruOrder(this.protectedLru, candidates, itemCount);
                TakeCandidatesInLruOrder(this.windowLru, candidates, itemCount);
            }

#if NET6_0_OR_GREATER
            foreach (var candidate in CollectionsMarshal.AsSpan(candidates))
#else
            foreach (var candidate in candidates)
#endif
            {
                this.TryRemove(candidate.Key);
            }
        }

        private bool TryAdd(K key, V value)
        {
            var node = policy.Create(key, value);

            if (this.dictionary.TryAdd(key, node))
            {
                AfterWrite(node);
                return true;
            }

            Disposer<V>.Dispose(node.Value);
            return false;
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out V? value))
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

        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            while (true)
            {
                if (this.TryGet(key, out V? value))
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

        public async ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out V? value))
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

        public async ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            while (true)
            {
                if (this.TryGet(key, out V? value))
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

        public bool TryGet(K key, [MaybeNullWhen(false)] out V value)
        {
            return TryGetImpl(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetImpl(K key, [MaybeNullWhen(false)] out V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                return GetOrDiscard(node, out value);
            }

            this.metrics.requestMissCount.Increment();

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetOrDiscard(N node, [MaybeNullWhen(false)] out V value)
        {
            if (!policy.IsExpired(node))
            {
                bool delayable = this.readBuffer.TryAdd(node) != BufferStatus.Full;

                if (this.drainStatus.ShouldDrain(delayable))
                {
                    TryScheduleDrain();
                }
                this.policy.OnRead(node);
                value = node.Value;
                return true;
            }

            // expired case, immediately remove from the dictionary
            TryRemove(node);
            this.metrics.requestMissCount.Increment();

            value = default;
            return false;
        }

        internal bool TryGetNode(K key, [MaybeNullWhen(false)] out N node)
        {
            return this.dictionary.TryGetValue(key, out node);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void TryRemove(N node)
        {
#if NET6_0_OR_GREATER
                if (this.dictionary.TryRemove(new KeyValuePair<K, N>(node.Key, node)))
#else
            // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
            if (((ICollection<KeyValuePair<K, N>>)this.dictionary).Remove(new KeyValuePair<K, N>(node.Key, node)))
#endif
            {
                node.WasRemoved = true;
                AfterWrite(node);
            }
        }

        public bool TryRemove(KeyValuePair<K, V> item)
        {
            if (this.dictionary.TryGetValue(item.Key, out var node))
            {
                lock (node)
                {
                    if (EqualityComparer<V>.Default.Equals(node.Value, item.Value))
                    {
                        var kvp = new KeyValuePair<K, N>(item.Key, node);

#if NET6_0_OR_GREATER
                        if (this.dictionary.TryRemove(kvp))
#else
                        // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
                        if (((ICollection<KeyValuePair<K, N>>)this.dictionary).Remove(kvp))
#endif
                        {
                            node.WasRemoved = true;
                            AfterWrite(node);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool TryRemove(K key, [MaybeNullWhen(false)] out V value)
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

        public bool TryRemove(K key)
        {
            return this.TryRemove(key, out var _);
        }

        public bool TryUpdate(K key, V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                return TryUpdateValue(node, value);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryUpdateValue(N node, V value)
        {
            lock (node)
            {
                if (!node.WasRemoved)
                {
                    node.Value = value;

                    // It's ok for this to be lossy, since the node is already tracked
                    // and we will just lose ordering/hit count, but not orphan the node.
                    this.writeBuffer.TryAdd(node);
                    TryScheduleDrain();
                    this.policy.OnWrite(node);
                    return true;
                }
            }

            return false;
        }

        public void DoMaintenance()
        {
            DrainBuffersSync();
        }

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

        private void AfterWrite(N node)
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

            int index = ClaimSecondaryBufferIndex();
            DrainPrimaryBuffersToSecondary(this.secondaryBuffers[index]);
            Maintenance(this.secondaryBuffers[index], node);
        }

        private void ScheduleAfterWrite()
        {
            var spinner = new SpinWait();
            int status = this.drainStatus.NonVolatileRead();
            while (true)
            {
                switch (status)
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
                            TryScheduleDrain();
                            return;
                        }
                        status = this.drainStatus.VolatileRead();
                        break;
                    case DrainStatus.ProcessingToRequired:
                        return;
                }
                spinner.SpinOnce();
            }
        }

        private void TryScheduleDrain()
        {
            while (true)
            {
                int status = this.drainStatus.VolatileRead();
                int activeDrains = Volatile.Read(ref this.activeDrainCount);

                switch (status)
                {
                    case DrainStatus.Idle:
                    case DrainStatus.Required:
                        if (activeDrains >= 2 || !this.drainStatus.Cas(status, DrainStatus.ProcessingToIdle))
                        {
                            return;
                        }
                        break;
                    case DrainStatus.ProcessingToRequired:
                        if (activeDrains >= 2)
                        {
                            return;
                        }
                        break;
                    default:
                        return;
                }

                if (Interlocked.CompareExchange(ref this.activeDrainCount, activeDrains + 1, activeDrains) == activeDrains)
                {
                    scheduler.Run(this.drainBuffers);
                    return;
                }
            }
        }

        internal void DrainBuffers()
        {
            try
            {
                int index = ClaimSecondaryBufferIndex();
                DrainPrimaryBuffersToSecondary(this.secondaryBuffers[index]);
                Maintenance(this.secondaryBuffers[index]);
            }
            finally
            {
                CompleteDrain();
            }
        }

        private void DrainBuffersSync()
        {
            do
            {
                int index = ClaimSecondaryBufferIndex();
                DrainPrimaryBuffersToSecondary(this.secondaryBuffers[index]);
                Maintenance(this.secondaryBuffers[index]);
            }
            while (HasPendingWork());

            this.drainStatus.NonVolatileWrite(DrainStatus.Idle);
        }

        private void Maintenance(SecondaryBufferSet secondaryBuffer, N? droppedWrite = null)
        {
            lock (maintenanceLock)
            {
                lock (secondaryBuffer.ReadLock)
                {
                    for (int i = 0; i < secondaryBuffer.ReadCount; i++)
                    {
                        this.cmSketch.Increment(secondaryBuffer.ReadBuffer[i].Key);
                    }

                    for (int i = 0; i < secondaryBuffer.ReadCount; i++)
                    {
                        OnAccess(secondaryBuffer.ReadBuffer[i]);
                        secondaryBuffer.ReadBuffer[i] = null!;
                    }

                    Volatile.Write(ref secondaryBuffer.ReadCount, 0);
                }

                lock (secondaryBuffer.WriteLock)
                {
                    for (int i = 0; i < secondaryBuffer.WriteCount; i++)
                    {
                        OnWrite(secondaryBuffer.WriteBuffer[i]);
                        secondaryBuffer.WriteBuffer[i] = null!;
                    }

                    Volatile.Write(ref secondaryBuffer.WriteCount, 0);
                }

                if (droppedWrite != null)
                {
                    OnWrite(droppedWrite);
                }

                policy.ExpireEntries(ref this);
                EvictEntries();
                this.capacity.OptimizePartitioning(this.metrics, this.cmSketch.ResetSampleSize);
                ReFitProtected();
            }
        }

        private void CompleteDrain()
        {
            int remainingDrains = Interlocked.Decrement(ref this.activeDrainCount);
            bool pendingWork = HasPendingWork();

            if (remainingDrains == 0)
            {
                this.drainStatus.NonVolatileWrite(pendingWork ? DrainStatus.Required : DrainStatus.Idle);
            }
            else
            {
                this.drainStatus.NonVolatileWrite(pendingWork ? DrainStatus.ProcessingToRequired : DrainStatus.ProcessingToIdle);
            }

            if (pendingWork)
            {
                TryScheduleDrain();
            }
        }

        private bool HasPendingWork()
        {
            return this.readBuffer.Count != 0 ||
                   this.writeBuffer.Count != 0 ||
                   Volatile.Read(ref this.secondaryBuffers[0].ReadCount) != 0 ||
                   Volatile.Read(ref this.secondaryBuffers[0].WriteCount) != 0 ||
                   Volatile.Read(ref this.secondaryBuffers[1].ReadCount) != 0 ||
                   Volatile.Read(ref this.secondaryBuffers[1].WriteCount) != 0;
        }

        private int ClaimSecondaryBufferIndex()
        {
            var spinner = new SpinWait();

            while (true)
            {
                int current = Volatile.Read(ref this.secondaryBufferIndex);
                int next = current ^ 1;

                if (Interlocked.CompareExchange(ref this.secondaryBufferIndex, next, current) == current)
                {
                    return current;
                }

                spinner.SpinOnce();
            }
        }

        private void DrainPrimaryBuffersToSecondary(SecondaryBufferSet secondaryBuffer)
        {
            lock (this.primaryReadBufferLock)
            {
                lock (secondaryBuffer.ReadLock)
                {
                    int availableCount = secondaryBuffer.ReadBuffer.Length - secondaryBuffer.ReadCount;
                    if (availableCount != 0)
                    {
                        var available = secondaryBuffer.ReadBuffer.AsSpanOrArray().Slice(secondaryBuffer.ReadCount, availableCount);
                        int drainCount = this.readBuffer.DrainTo(available);
                        Volatile.Write(ref secondaryBuffer.ReadCount, secondaryBuffer.ReadCount + drainCount);
                    }
                }
            }

            lock (this.primaryWriteBufferLock)
            {
                lock (secondaryBuffer.WriteLock)
                {
                    int availableCount = secondaryBuffer.WriteBuffer.Length - secondaryBuffer.WriteCount;
                    if (availableCount != 0)
                    {
                        var available = secondaryBuffer.WriteBuffer.AsSpanOrArray().Slice(secondaryBuffer.WriteCount, availableCount);
                        int drainCount = this.writeBuffer.DrainTo(available);
                        Volatile.Write(ref secondaryBuffer.WriteCount, secondaryBuffer.WriteCount + drainCount);
                    }
                }
            }
        }

        private void ClearSecondaryBuffers()
        {
            ClearSecondaryBuffer(this.secondaryBuffers[0]);
            ClearSecondaryBuffer(this.secondaryBuffers[1]);
        }

        private static void ClearSecondaryBuffer(SecondaryBufferSet secondaryBuffer)
        {
            lock (secondaryBuffer.ReadLock)
            {
                Array.Clear(secondaryBuffer.ReadBuffer, 0, secondaryBuffer.ReadCount);
                Volatile.Write(ref secondaryBuffer.ReadCount, 0);
            }

            lock (secondaryBuffer.WriteLock)
            {
                Array.Clear(secondaryBuffer.WriteBuffer, 0, secondaryBuffer.WriteCount);
                Volatile.Write(ref secondaryBuffer.WriteCount, 0);
            }
        }

        private void OnAccess(N node)
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
                    Debug.Assert(node.list == this.windowLru);
                    this.windowLru.MoveToEnd(node);
                    break;
                case Position.Probation:
                    Debug.Assert(node.list == this.probationLru);
                    PromoteProbation(node);
                    break;
                case Position.Protected:
                    Debug.Assert(node.list == this.protectedLru);
                    this.protectedLru.MoveToEnd(node);
                    break;
            }

            policy.AfterRead(node);
        }

        private void OnWrite(N node)
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
                        Debug.Assert(node.list == this.windowLru);
                        this.windowLru.MoveToEnd(node);
                        this.metrics.updatedCount++;
                    }
                    break;
                case Position.Probation:
                    Debug.Assert(node.list == this.probationLru);
                    PromoteProbation(node);
                    this.metrics.updatedCount++;
                    break;
                case Position.Protected:
                    Debug.Assert(node.list == this.protectedLru);
                    this.protectedLru.MoveToEnd(node);
                    this.metrics.updatedCount++;
                    break;
            }

            policy.AfterWrite(node);
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

        private LfuNode<K, V>? EvictFromWindow()
        {
            LfuNode<K, V>? first = null;

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
            public LfuNode<K, V>? node;
            public int freq;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EvictIterator(CmSketch<K> sketch, LfuNode<K, V>? node)
            {
                this.sketch = sketch;
                this.node = node;
                freq = node == null ? -1 : sketch.EstimateFrequency(node.Key);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Next()
            {
                node = node!.Next;

                if (node != null)
                {
                    freq = sketch.EstimateFrequency(node.Key);
                }
            }
        }

        private void EvictFromMain(LfuNode<K, V>? candidateNode)
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
                    Evict(candidate.node!);
                    break;
                }

                if (candidate.node!.WasRemoved)
                {
                    var evictee = candidate.node;
                    candidate.Next();
                    Evict(evictee);
                    continue;
                }

                if (victim.node!.WasRemoved)
                {
                    var evictee = victim.node;
                    victim.Next();
                    Evict(evictee);
                    continue;
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

            // TODO: random factor when candidate freq < 5
            return candidateFreq > victimFreq;
        }

        internal void Evict(LfuNode<K, V> evictee)
        {
            evictee.WasRemoved = true;
            evictee.WasDeleted = true;

            // This handles the case where the same key exists in the write buffer both
            // as added and removed. Remove via KVP ensures we don't remove added nodes.
            var kvp = new KeyValuePair<K, N>(evictee.Key, (N)evictee);
#if NET6_0_OR_GREATER
            this.dictionary.TryRemove(kvp);
#else
            ((ICollection<KeyValuePair<K, N>>)this.dictionary).Remove(kvp);
#endif
            evictee.list?.Remove(evictee);
            Disposer<V>.Dispose(evictee.Value);
            this.metrics.evictedCount++;

            this.policy.OnEvict((N)evictee);
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

        private sealed class SecondaryBufferSet
        {
            public SecondaryBufferSet(int readBufferCapacity, int writeBufferCapacity)
            {
                this.ReadBuffer = new N[readBufferCapacity];
                this.WriteBuffer = new N[writeBufferCapacity];
            }

            public N[] ReadBuffer { get; }

            public int ReadCount;

#if NET9_0_OR_GREATER
            public Lock ReadLock { get; } = new();
#else
            public object ReadLock { get; } = new();
#endif

            public N[] WriteBuffer { get; }

            public int WriteCount;

#if NET9_0_OR_GREATER
            public Lock WriteLock { get; } = new();
#else
            public object WriteLock { get; } = new();
#endif
        }

        [DebuggerDisplay("{Format(),nq}")]
        internal class DrainStatus
        {
            public const int Idle = 0;
            public const int Required = 1;
            public const int ProcessingToIdle = 2;
            public const int ProcessingToRequired = 3;

            private PaddedInt drainStatus; // mutable struct, don't mark readonly

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ShouldDrain(bool delayable)
            {
                int status = this.NonVolatileRead();
                return status switch
                {
                    Idle => !delayable,
                    Required => true,
                    // ProcessingToIdle or ProcessingToRequired => false, undefined not reachable
                    _ => false,
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VolatileWrite(int newStatus)
            {
                Volatile.Write(ref this.drainStatus.Value, newStatus);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void NonVolatileWrite(int newStatus)
            {
                this.drainStatus.Value = newStatus;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Cas(int oldStatus, int newStatus)
            {
                return Interlocked.CompareExchange(ref this.drainStatus.Value, newStatus, oldStatus) == oldStatus;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int VolatileRead()
            {
                return Volatile.Read(ref this.drainStatus.Value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int NonVolatileRead()
            {
                return this.drainStatus.Value;
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

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAlternateLookup<TAlternateKey, K, V> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            if (!this.dictionary.IsCompatibleKey<TAlternateKey, K, N>())
            {
                Throw.IncompatibleComparer();
            }

            return new AlternateLookup<TAlternateKey>(this);
        }

        ///<inheritdoc/>
        public bool TryGetAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            if (this.dictionary.IsCompatibleKey<TAlternateKey, K, N>())
            {
                lookup = new AlternateLookup<TAlternateKey>(this);
                return true;
            }

            lookup = default;
            return false;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncAlternateLookup<TAlternateKey, K, V> GetAsyncAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            if (!this.dictionary.IsCompatibleKey<TAlternateKey, K, N>())
            {
                Throw.IncompatibleComparer();
            }

            return new AlternateLookup<TAlternateKey>(this);
        }

        ///<inheritdoc/>
        public bool TryGetAsyncAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAsyncAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            if (this.dictionary.IsCompatibleKey<TAlternateKey, K, N>())
            {
                lookup = new AlternateLookup<TAlternateKey>(this);
                return true;
            }

            lookup = default;
            return false;
        }

        internal readonly struct AlternateLookup<TAlternateKey> : IAlternateLookup<TAlternateKey, K, V>, IAsyncAlternateLookup<TAlternateKey, K, V>
            where TAlternateKey : notnull, allows ref struct
        {
            internal AlternateLookup(ConcurrentLfuCore<K, V, N, P> lfu)
            {
                Debug.Assert(lfu.dictionary.IsCompatibleKey<TAlternateKey, K, N>());
                this.Lfu = lfu;
                this.Alternate = lfu.dictionary.GetAlternateLookup<TAlternateKey>();
                this.Comparer = lfu.dictionary.GetAlternateComparer<TAlternateKey, K, N>();
            }

            internal ConcurrentLfuCore<K, V, N, P> Lfu { get; }

            internal ConcurrentDictionary<K, N>.AlternateLookup<TAlternateKey> Alternate { get; }

            internal IAlternateEqualityComparer<TAlternateKey, K> Comparer { get; }

            public bool TryGet(TAlternateKey key, [MaybeNullWhen(false)] out V value)
            {
                if (this.Alternate.TryGetValue(key, out var node))
                {
                    return this.Lfu.GetOrDiscard(node, out value);
                }

                this.Lfu.metrics.requestMissCount.Increment();

                value = default;
                return false;
            }

            public bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out K actualKey, [MaybeNullWhen(false)] out V value)
            {
                if (this.Alternate.TryRemove(key, out actualKey, out var node))
                {
                    node.WasRemoved = true;
                    this.Lfu.AfterWrite(node);
                    value = node.Value;
                    return true;
                }

                actualKey = default;
                value = default;
                return false;
            }

            public bool TryUpdate(TAlternateKey key, V value)
            {
                if (this.Alternate.TryGetValue(key, out var node))
                {
                    return this.Lfu.TryUpdateValue(node, value);
                }

                return false;
            }

            public void AddOrUpdate(TAlternateKey key, V value)
            {
                K actualKey = default!;
                bool hasActualKey = false;

                while (true)
                {
                    if (this.TryUpdate(key, value))
                    {
                        return;
                    }

                    if (!hasActualKey)
                    {
                        actualKey = this.Comparer.Create(key);
                        hasActualKey = true;
                    }

                    if (this.Lfu.TryAdd(actualKey, value))
                    {
                        return;
                    }
                }
            }

            public V GetOrAdd(TAlternateKey key, Func<K, V> valueFactory)
            {
                while (true)
                {
                    if (this.TryGet(key, out var value))
                    {
                        return value;
                    }

                    K actualKey = this.Comparer.Create(key);

                    value = valueFactory(actualKey);
                    if (this.Lfu.TryAdd(actualKey, value))
                    {
                        return value;
                    }
                }
            }

            public V GetOrAdd<TArg>(TAlternateKey key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
            {
                while (true)
                {
                    if (this.TryGet(key, out var value))
                    {
                        return value;
                    }

                    K actualKey = this.Comparer.Create(key);

                    value = valueFactory(actualKey, factoryArgument);
                    if (this.Lfu.TryAdd(actualKey, value))
                    {
                        return value;
                    }
                }
            }

            public ValueTask<V> GetOrAddAsync(TAlternateKey key, Func<K, Task<V>> valueFactory)
            {
                if (this.TryGet(key, out var value))
                {
                    return new ValueTask<V>(value);
                }

                K actualKey = this.Comparer.Create(key);
                Task<V> task = valueFactory(actualKey);

                return GetOrAddAsyncSlow(actualKey, task);
            }

            public ValueTask<V> GetOrAddAsync<TArg>(TAlternateKey key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
            {
                if (this.TryGet(key, out var value))
                {
                    return new ValueTask<V>(value);
                }

                K actualKey = this.Comparer.Create(key);
                Task<V> task = valueFactory(actualKey, factoryArgument);

                return GetOrAddAsyncSlow(actualKey, task);
            }

            // Since TAlternateKey can be a ref struct, we can't use async/await in the public GetOrAddAsync methods,
            // so we delegate to this private async method after the value factory is invoked.
            private async ValueTask<V> GetOrAddAsyncSlow(K actualKey, Task<V> task)
            {
                V value = await task.ConfigureAwait(false);

                while (true)
                {
                    if (this.Lfu.TryAdd(actualKey, value))
                    {
                        return value;
                    }

                    // Another thread added a value for this key first, retrieve it.
                    if (this.Lfu.TryGet(actualKey, out V? existing))
                    {
                        return existing;
                    }
                }
            }
        }
#endif

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
            sb.Append(']');

            return sb.ToString();
        }
#endif
    }

    // Explicit layout cannot be a generic class member
    [StructLayout(LayoutKind.Explicit, Size = 2 * Padding.CACHE_LINE_SIZE)]
    internal struct PaddedInt
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public int Value;
    }
}
