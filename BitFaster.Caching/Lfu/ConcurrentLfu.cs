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
using BitFaster.Caching.Concurrent;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

#if !NETSTANDARD2_0
using System.Buffers;
#endif

#if DEBUG
using System.Linq;
using System.Text;
#endif

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// An LFU cache with a W-TinyLfu eviction policy.
    /// </summary>
    /// <remarks>
    /// Based on Caffeine written by Ben Manes.
    /// https://www.apache.org/licenses/LICENSE-2.0
    /// </remarks>
    [DebuggerTypeProxy(typeof(ConcurrentLfu<,>.LfuDebugView))]
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public sealed class ConcurrentLfu<K, V> : ICache<K, V>, IAsyncCache<K, V>, IBoundedPolicy
    {
        private const int MaxWriteBufferRetries = 64;

        private readonly ConcurrentDictionary<K, LfuNode<K, V>> dictionary;

        private readonly StripedMpscBuffer<LfuNode<K, V>> readBuffer;
        private readonly MpscBoundedBuffer<LfuNode<K, V>> writeBuffer;

        private readonly CacheMetrics metrics = new CacheMetrics();

        private readonly CmSketch<K> cmSketch;

        private readonly LfuNodeList<K, V> windowLru;
        private readonly LfuNodeList<K, V> probationLru;
        private readonly LfuNodeList<K, V> protectedLru;

        private readonly LfuCapacityPartition capacity;

        private readonly DrainStatus drainStatus = new DrainStatus();
        private readonly object maintenanceLock = new object();

        private readonly IScheduler scheduler;

#if NETSTANDARD2_0
        private readonly LfuNode<K, V>[] drainBuffer;
#endif

        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified capacity.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public ConcurrentLfu(int capacity)
            : this(Defaults.ConcurrencyLevel, capacity, new ThreadPoolScheduler(), EqualityComparer<K>.Default, LfuBufferSize.Default(Defaults.ConcurrencyLevel, capacity))
        {        
        }

        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified concurrencyLevel, capacity, scheduler, equality comparer and buffer size.
        /// </summary>
        /// <param name="concurrencyLevel">The concurrency level.</param>
        /// <param name="capacity">The capacity.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <param name="comparer">The equality comparer.</param>
        /// <param name="bufferSize">The buffer size.</param>
        public ConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, LfuBufferSize bufferSize)
        {
            this.dictionary = new ConcurrentDictionary<K, LfuNode<K, V>>(concurrencyLevel, capacity, comparer);

            this.readBuffer = new StripedMpscBuffer<LfuNode<K, V>>(bufferSize.Read);

            // Cap the write buffer to 10% of the cache size, or BufferSize. Whichever is smaller.
            int writeBufferSize = Math.Min(capacity / 10, 128);
            this.writeBuffer = new MpscBoundedBuffer<LfuNode<K, V>>(writeBufferSize);

            this.cmSketch = new CmSketch<K>(1, comparer);
            this.cmSketch.EnsureCapacity(capacity);
            this.windowLru = new LfuNodeList<K, V>();
            this.probationLru = new LfuNodeList<K, V>();
            this.protectedLru = new LfuNodeList<K, V>();

            this.capacity = new LfuCapacityPartition(capacity);

            this.scheduler = scheduler;

#if NETSTANDARD2_0
            this.drainBuffer = new LfuNode<K, V>[this.readBuffer.Capacity];
#endif
        }

        ///<inheritdoc/>
        public int Count => this.dictionary.Count;

        ///<inheritdoc/>
        public int Capacity => this.capacity.Capacity;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => new Optional<ICacheMetrics>(this.metrics);

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => Optional<ICacheEvents<K, V>>.None();

        ///<inheritdoc/>
        public CachePolicy Policy => new CachePolicy(new Optional<IBoundedPolicy>(this), Optional<ITimePolicy>.None());

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

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out V value))
                {
                    return value;
                }

                var node = new LfuNode<K, V>(key, valueFactory(key));
                if (this.dictionary.TryAdd(key, node))
                {
                    AfterWrite(node);
                    return node.Value;
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

                var node = new LfuNode<K, V>(key, await valueFactory(key).ConfigureAwait(false));
                if (this.dictionary.TryAdd(key, node))
                {
                    AfterWrite(node);
                    return node.Value;
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

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            if (this.dictionary.TryRemove(key, out var node))
            {
                node.WasRemoved = true;
                AfterWrite(node);
                return true;
            }

            return false;
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
        /// Synchronously perform all pending maintenance. Draining the read and write buffers then
        /// use the eviction policy to preserve bounded size and remove expired items.
        /// </summary>
        public void PendingMaintenance()
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
                candidates.Add(curr);
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
            var localDrainBuffer = RentDrainBuffer();

            // extract to a buffer before doing book keeping work, ~2x faster
            int readCount = readBuffer.DrainTo(localDrainBuffer);

            for (int i = 0; i < readCount; i++)
            {
                this.cmSketch.Increment(localDrainBuffer[i].Key);
            }

            for (int i = 0; i < readCount; i++)
            {
                OnAccess(localDrainBuffer[i]);
            }

            count = this.writeBuffer.DrainTo(new ArraySegment<LfuNode<K, V>>(localDrainBuffer));

            for (int i = 0; i < writeCount; i++)
            {
                OnWrite(localDrainBuffer[i]);
            }

            // we are done only when both buffers are empty
            var done = readCount == 0 & writeCount == 0;

            if (droppedWrite != null)
            {
                OnWrite(droppedWrite);
                done = true;
            }

            ReturnDrainBuffer(localDrainBuffer);

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
                if (node.list != null)
                {
                    node.list.Remove(node);
                }

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

                if (first == null)
                {
                    first = node;
                }

                this.probationLru.AddLast(node);
                node.Position = Position.Probation;
            }

            return first;
        }

        private void EvictFromMain(LfuNode<K, V> candidate)
        {
            var victim = this.probationLru.First; // victims are LRU position in probation

            // first pass: admit candidates
            while (this.windowLru.Count + this.probationLru.Count + this.protectedLru.Count > this.Capacity)
            {
                // bail when we run out of options
                if (candidate == null | victim == null | victim == candidate)
                {
                    break;
                }

                // Evict the entry with the lowest frequency
                if (AdmitCandidate(candidate.Key, victim.Key))
                {
                    var evictee = victim;

                    // victim is initialized to first, and iterates forwards
                    victim = victim.Next;
                    candidate = candidate.Next;

                    Evict(evictee);
                }
                else
                {
                    var evictee = candidate;

                    // candidate is initialized to last, and iterates backwards
                    candidate = candidate.Next;

                    Evict(evictee);
                }
            }

            // 2nd pass: remove probation items in LRU order, evict lowest frequency
            while (this.windowLru.Count + this.probationLru.Count + this.protectedLru.Count > this.Capacity)
            {
                victim = this.probationLru.First;
                var victim2 = victim.Next;

                if (AdmitCandidate(victim.Key, victim2.Key))
                {
                    Evict(victim2);
                }
                else
                {
                    Evict(victim);
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

        private LfuNode<K, V>[] RentDrainBuffer()
        {
#if !NETSTANDARD2_0
            return ArrayPool<LfuNode<K, V>>.Shared.Rent(this.readBuffer.Capacity);
#else
            return drainBuffer;
#endif
        }

        private void ReturnDrainBuffer(LfuNode<K, V>[] localDrainBuffer)
        {
#if !NETSTANDARD2_0
            ArrayPool<LfuNode<K, V>>.Shared.Return(localDrainBuffer);
#endif
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
                switch (status)
                {
                    case Idle:
                        return !delayable;
                    case Required:
                        return true;
                    case ProcessingToIdle:
                    case ProcessingToRequired:
                        return false;
                    default:
                        throw new InvalidOperationException();
                }
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
            public LongAdder requestMissCount = new LongAdder();
            public long updatedCount;
            public long evictedCount;

            public double HitRatio => (double)requestHitCount / (double)Total;

            public long Total => requestHitCount + requestMissCount.Sum();

            public long Hits => requestHitCount;

            public long Misses => requestMissCount.Sum();

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

            public StripedMpscBuffer<LfuNode<K, V>> WriteBuffer => this.lfu.writeBuffer;

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
