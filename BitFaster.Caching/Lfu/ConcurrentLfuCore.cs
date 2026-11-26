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
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;


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

    internal struct ConcurrentLfuCore<K, V, N, P, E> : IBoundedPolicy
        where K : notnull
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N, E>
        where E : struct, IEventPolicy<K, V>
    {
        private const int MaxWriteBufferRetries = 64;

        private const int DefaultBufferSize = 128;

        private readonly ConcurrentDictionary<K, N> dictionary;

        internal readonly StripedMpscBuffer<N> readBuffer;
        internal readonly MpscBoundedBuffer<N> writeBuffer;

        private readonly CacheMetrics metrics = new();

        private readonly CmSketch<K> cmSketch;

        internal readonly DrainStatus drainStatus = new();

#if NET9_0_OR_GREATER
        private readonly Lock maintenanceLock = new();
#else
        private readonly object maintenanceLock = new();
#endif

        private readonly IScheduler scheduler;
        private readonly Action drainBuffers;

        private readonly N[] drainBuffer;

        internal P policy;

        /// <summary>
        /// The event policy.
        /// </summary>
        /// <remarks>
        /// Since E is a struct, making it readonly will force the runtime to make defensive copies
        /// if mutate methods are called. Therefore, field must be mutable to maintain count.
        /// </remarks>
        internal E eventPolicy;

        public ConcurrentLfuCore(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, Action drainBuffers, P policy, E eventPolicy)
        {
            if (capacity < 3)
                Throw.ArgOutOfRange(nameof(capacity));

            int dictionaryCapacity = ConcurrentDictionarySize.Estimate(capacity);
            this.dictionary = new(concurrencyLevel, dictionaryCapacity, comparer);

            // On .NET 8+, -1 can be used for default conc level. concurrencyLevel is guarded by the dictionary ctor that will not throw in this case.
#if NET8_0_OR_GREATER
            if (concurrencyLevel == -1)
            { 
                concurrencyLevel = Defaults.ConcurrencyLevel;
            }
#endif

            // cap concurrency at proc count * 2
            int readStripes = Math.Min(BitOps.CeilingPowerOfTwo(concurrencyLevel), BitOps.CeilingPowerOfTwo(Environment.ProcessorCount * 2));
            this.readBuffer = new(readStripes, DefaultBufferSize);

            // Cap the write buffer to the cache size, or 128. Whichever is smaller.
            int writeBufferSize = Math.Min(BitOps.CeilingPowerOfTwo(capacity), 128);
            this.writeBuffer = new(writeBufferSize);

            this.cmSketch = new CmSketch<K>(capacity, comparer);
            this.scheduler = scheduler;

            this.drainBuffer = new N[this.readBuffer.Capacity];

            this.drainBuffers = drainBuffers;

            this.policy = policy;
            this.policy.Initialize(capacity);

            this.eventPolicy = eventPolicy;
        }

        // No lock count: https://arbel.net/2013/02/03/best-practices-for-using-concurrentdictionary/
        public int Count => this.dictionary.Skip(0).Count();

        public int Capacity => this.policy.Eviction.Capacity.Capacity;

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
            Trim(int.MaxValue, ItemRemovedReason.Cleared);

            lock (maintenanceLock)
            {
                this.readBuffer.Clear();
                this.writeBuffer.Clear();
                this.cmSketch.Clear();
            }
        }

        public void Trim(int itemCount)
        {
            Trim(itemCount, ItemRemovedReason.Trimmed);
        }

        private void Trim(int itemCount, ItemRemovedReason reason)
        {
            lock (maintenanceLock)
            {
                Maintenance(reason: reason);

                var candidates = this.policy.Eviction.GetTrimCandidates(itemCount);

#if NET6_0_OR_GREATER
            foreach (var candidate in CollectionsMarshal.AsSpan(candidates))
#else
                foreach (var candidate in candidates)
#endif
                {
                    Evict(candidate, reason);
                }
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
#if NET9_0_OR_GREATER
            where TArg : allows ref struct
#endif
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
            bool requiresWrite = false;
            try
            {
                lock (node)
                {
                    if (!node.WasRemoved)
                    {
                        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
                        V oldValue = node.Value;
#endif
                        requiresWrite = this.policy.UpdateValue(node, value);

                        if (!requiresWrite)
                        {
                            // For non-weighted it's ok for this to be lossy, since the node is already tracked
                            // and we will just lose ordering/hit count, but not orphan the node.
                            this.writeBuffer.TryAdd(node);
                            TryScheduleDrain();
                        }
                        this.policy.OnWrite(node);

                        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
                        EventPolicyDispatch.OnUpdatedEvent(this, node.Key, oldValue, value);
#endif
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                // Weighted policy may require a write to update the total weight of the cache.
                // If the value was updated, but the weight did not change, then no write is required.
                if (requiresWrite)
                {
                    AfterWrite(node);
                }
            }
        }

        public void DoMaintenance()
        {
            DrainBuffers();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.dictionary)
            {
                yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.Value);
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
            if (this.drainStatus.VolatileRead() >= DrainStatus.ProcessingToIdle)
            {
                return;
            }

#if NET9_0_OR_GREATER
            if (maintenanceLock.TryEnter())
#else
            bool lockTaken = false;
            Monitor.TryEnter(maintenanceLock, ref lockTaken);
            if (lockTaken)
#endif
            {
                try
                {
                    int status = this.drainStatus.NonVolatileRead();

                    if (status >= DrainStatus.ProcessingToIdle)
                    {
                        return;
                    }

                    this.drainStatus.VolatileWrite(DrainStatus.ProcessingToIdle);
                    scheduler.Run(this.drainBuffers);
                }
                finally
                {
#if NET9_0_OR_GREATER
                    maintenanceLock.Exit();
#else
                    if (lockTaken)
                    {
                        Monitor.Exit(maintenanceLock);
                    }
#endif
                }
            }
        }

        internal void DrainBuffers()
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

            if (this.drainStatus.VolatileRead() == DrainStatus.Required)
            {
                TryScheduleDrain();
            }
        }

        private bool Maintenance(N? droppedWrite = null, ItemRemovedReason reason = ItemRemovedReason.Evicted)
        {
            this.drainStatus.VolatileWrite(DrainStatus.ProcessingToIdle);

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

            policy.ExpireEntries(ref this);
            policy.EvictEntries(ref this, this.cmSketch, reason);
            policy.OptimizePartitioning(this.metrics, this.cmSketch.ResetSampleSize);

            // Reset to idle if either
            // 1. We drained both input buffers (all work done)
            // 2. or scheduler is foreground (since don't run continuously on the foreground)
            if ((done || !scheduler.IsBackground) &&
                (this.drainStatus.NonVolatileRead() != DrainStatus.ProcessingToIdle ||
                !this.drainStatus.Cas(DrainStatus.ProcessingToIdle, DrainStatus.Idle)))
            {
                this.drainStatus.NonVolatileWrite(DrainStatus.Required);
            }

            return done;
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

            policy.AfterRead(node);
        }

        private void OnWrite(N node)
        {
            // Nodes can be removed while they are in the write buffer, in which case they should
            // not be added back into the LRU.
            if (node.WasRemoved)
            {
                policy.OnEvict(node);

                if (!node.WasDeleted)
                {
                    // if a write is in the buffer and is then removed in the buffer, it will enter OnWrite twice.
                    // we mark as deleted to avoid double counting/disposing it
                    EventPolicyDispatch.OnRemovedEvent(this, node, ItemRemovedReason.Removed);
                    this.metrics.evictedCount++;
                    Disposer<V>.Dispose(node.Value);
                    node.WasDeleted = true;
                }

                return;
            }

            this.cmSketch.Increment(node.Key);

            if (policy.AfterWrite(ref this, node))
            {
                this.metrics.updatedCount++;
            }
        }

        internal void Evict(LfuNode<K, V> evictee, ItemRemovedReason reason)
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
            this.policy.OnEvict((N)evictee);
            EventPolicyDispatch.OnRemovedEvent(this, evictee, reason);
            Disposer<V>.Dispose(evictee.Value);
            this.metrics.evictedCount++;
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

        // enable JIT to elide unused code.
        private static class EventPolicyDispatch
        {
            private static readonly bool IsEnabled = typeof(E) == typeof(EventPolicy<K, V>);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void OnRemovedEvent(ConcurrentLfuCore<K, V, N, P, E> cache, LfuNode<K, V> node, ItemRemovedReason reason)
            {
                if (IsEnabled)
                {
                    cache.eventPolicy.OnItemRemoved(node.Key, node.Value, reason);
                }
            }
#if NETCOREAPP3_0_OR_GREATER
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void OnUpdatedEvent(ConcurrentLfuCore<K, V, N, P, E> cache, K key, V oldValue, V newValue)
            {
                if (IsEnabled)
                {
                    cache.eventPolicy.OnItemUpdated(key, oldValue, newValue);
                }
            }
#endif
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
            internal AlternateLookup(ConcurrentLfuCore<K, V, N, P, E> lfu)
            {
                Debug.Assert(lfu.dictionary.IsCompatibleKey<TAlternateKey, K, N>());
                this.Lfu = lfu;
                this.Alternate = lfu.dictionary.GetAlternateLookup<TAlternateKey>();
                this.Comparer = lfu.dictionary.GetAlternateComparer<TAlternateKey, K, N>();
            }

            internal ConcurrentLfuCore<K, V, N, P, E> Lfu { get; }

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
                where TArg : allows ref struct
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
            return this.policy.Eviction.FormatLfuString();
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
