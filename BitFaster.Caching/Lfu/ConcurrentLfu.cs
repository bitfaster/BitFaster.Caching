﻿using System;

#if NETSTANDARD2_0
#else
using System.Buffers;
#endif

#if DEBUG
using System.Linq;
using System.Text;
#endif

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// An LFU cache with a W-TinyLfu eviction policy.
    /// </summary>
    /// <remarks>
    /// Based on Caffeine written by Ben Manes.
    /// https://www.apache.org/licenses/LICENSE-2.0
    /// </remarks>
    public class ConcurrentLfu<K, V> : ICache<K, V>, IBoundedPolicy
    {
        private const int MaxWriteBufferRetries = 100;
        private const int TakeBufferSize = 1024;

        public const int BufferSize = 128;

        private readonly ConcurrentDictionary<K, LinkedListNode<LfuNode<K, V>>> dictionary;

        private readonly BoundedBuffer<LinkedListNode<LfuNode<K, V>>> readBuffer;
        private readonly BoundedBuffer<LinkedListNode<LfuNode<K, V>>> writeBuffer;

        private readonly CacheMetrics metrics = new CacheMetrics();

        private readonly CmSketch<K> cmSketch;

        private readonly LinkedList<LfuNode<K, V>> windowLru;
        private readonly LinkedList<LfuNode<K, V>> probationLru;
        private readonly LinkedList<LfuNode<K, V>> protectedLru;

        private readonly LfuCapacityPartition capacity;

        private readonly DrainStatus drainStatus = new DrainStatus();
        private readonly object maintenanceLock = new object();

        private readonly IScheduler scheduler;

#if NETSTANDARD2_0
        private readonly LinkedListNode<LfuNode<K, V>>[] localDrainBuffer = new LinkedListNode<LfuNode<K, V>>[TakeBufferSize];
#endif

        public ConcurrentLfu(int capacity)
            : this(capacity, new ThreadPoolScheduler())
        {        
        }

        public ConcurrentLfu(int capacity, IScheduler scheduler)
        {
            var comparer = EqualityComparer<K>.Default;
            this.dictionary = new ConcurrentDictionary<K, LinkedListNode<LfuNode<K, V>>>(Defaults.ConcurrencyLevel, capacity, comparer);
            this.readBuffer = new BoundedBuffer<LinkedListNode<LfuNode<K, V>>>(BufferSize);
            this.writeBuffer = new BoundedBuffer<LinkedListNode<LfuNode<K, V>>>(BufferSize);
            this.cmSketch = new CmSketch<K>(1, comparer);
            this.cmSketch.EnsureCapacity(capacity);
            this.windowLru = new LinkedList<LfuNode<K, V>>();
            this.probationLru = new LinkedList<LfuNode<K, V>>();
            this.protectedLru = new LinkedList<LfuNode<K, V>>();

            this.capacity = new LfuCapacityPartition(capacity);

            this.scheduler = scheduler;
        }

        public int Count => this.dictionary.Count;

        public int Capacity => this.capacity.Capacity;

        public Optional<ICacheMetrics> Metrics => new Optional<ICacheMetrics>(this.metrics);

        public Optional<ICacheEvents<K, V>> Events => Optional<ICacheEvents<K, V>>.None();

        public CachePolicy Policy => new CachePolicy(new Optional<IBoundedPolicy>(this), Optional<ITimePolicy>.None());

        public ICollection<K> Keys => this.dictionary.Keys;

        public IScheduler Scheduler => scheduler;

        public void AddOrUpdate(K key, V value)
        {
            while (true)
            {
                if (TryUpdate(key, value))
                {
                    return;
                }

                var node = new LinkedListNode<LfuNode<K, V>>(new LfuNode<K, V>(key, value));
                if (this.dictionary.TryAdd(key, node))
                {
                    AfterWrite(node);
                    return;
                }
            }
        }

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

        public void Trim(int itemCount)
        {
            itemCount = Math.Min(itemCount, this.Count);
            var candidates = new List<LinkedListNode<LfuNode<K, V>>>(itemCount);

            // TODO: this is LRU order eviction, Caffeine void evictFromMain(int candidates) is based on frequency
            lock (maintenanceLock)
            {
                // walk in lru order, get itemCount keys to evict
                TakeCandidatesInLruOrder(this.probationLru, candidates, itemCount);
                TakeCandidatesInLruOrder(this.protectedLru, candidates, itemCount);
                TakeCandidatesInLruOrder(this.windowLru, candidates, itemCount);
            }

            foreach (var candidate in candidates)
            {
                this.TryRemove(candidate.Value.Key);
            }
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {
                if (this.TryGet(key, out V value))
                {
                    return value;
                }

                var node = new LinkedListNode<LfuNode<K, V>>(new LfuNode<K, V>(key, valueFactory(key)));
                if (this.dictionary.TryAdd(key, node))
                {
                    AfterWrite(node);
                    return node.Value.Value;
                }
            }
        }

        public bool TryGet(K key, out V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                bool delayable = this.readBuffer.TryAdd(node);

                if (this.drainStatus.ShouldDrain(delayable))
                { 
                    TryScheduleDrain();
                }
                value = node.Value.Value;               
                return true;
            }

            Interlocked.Increment(ref this.metrics.requestMissCount);

            value = default;
            return false;
        }

        public bool TryRemove(K key)
        {
            if (this.dictionary.TryRemove(key, out var node))
            {
                node.Value.WasRemoved = true;
                AfterWrite(node);
                return true;
            }

            return false;
        }

        public bool TryUpdate(K key, V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                node.Value.Value = value;

                // It's ok for this to be lossy, since the node is already tracked
                // and we will just lose ordering/hit count, but not orphan the node.
                this.writeBuffer.TryAdd(node);
                TryScheduleDrain();
                return true;
            }

            return false;
        }

        public void PendingMaintenance()
        {
            DrainBuffers();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.dictionary)
            {
                yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.Value.Value);
            }
        }

        private static void TakeCandidatesInLruOrder(LinkedList<LfuNode<K, V>> lru, List<LinkedListNode<LfuNode<K, V>>> candidates, int itemCount)
        {
            var curr = lru.First;

            while (candidates.Count < itemCount && curr != null)
            {
                candidates.Add(curr);
                curr = curr.Next;
            }
        }

        private void AfterWrite(LinkedListNode<LfuNode<K, V>> node)
        {
            var spinner = new SpinWait();

            for (int i = 0; i < MaxWriteBufferRetries; i++)
            {
                if (writeBuffer.TryAdd(node))
                {
                    ScheduleAfterWrite();
                    return;
                }

                TryScheduleDrain();

                spinner.SpinOnce();
            }

            lock (this.maintenanceLock)
            {
                Maintenance();
            }
        }

        private void ScheduleAfterWrite()
        {
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

        private bool Maintenance()
        {
            this.drainStatus.Set(DrainStatus.ProcessingToIdle);

#if !NETSTANDARD2_0
            var localDrainBuffer = ArrayPool<LinkedListNode<LfuNode<K, V>>>.Shared.Rent(TakeBufferSize);
#endif
            bool wasDrained = false;
            int count = 0;

            // extract to a buffer before doing book keeping work, ~2x faster
            while (this.readBuffer.TryTake(out var node) && count < TakeBufferSize)
            {
                localDrainBuffer[count++] = node;
            }

            for (int i = 0; i < count; i++)
            {
                this.cmSketch.Increment(localDrainBuffer[i].Value.Key);
            }

            for (int i = 0; i < count; i++)
            {
                OnAccess(localDrainBuffer[i]);
            }

            wasDrained = count == 0;

            while (this.writeBuffer.TryTake(out var node))
            {
                OnWrite(node);
            }

#if !NETSTANDARD2_0
            ArrayPool<LinkedListNode<LfuNode<K, V>>>.Shared.Return(localDrainBuffer);
#endif

            // TODO: hill climb

            // Reset to idle if either
            // 1. We drained both input buffers (all work done)
            // 2. or scheduler is foreground (since don't run continuously on the foreground)
            if ((wasDrained || !scheduler.IsBackground) &&
                (this.drainStatus.Status() != DrainStatus.ProcessingToIdle ||
                !this.drainStatus.Cas(DrainStatus.ProcessingToIdle, DrainStatus.Idle)))
            {
                this.drainStatus.Set(DrainStatus.Required);
            }

            return wasDrained;
        }

        private void OnAccess(LinkedListNode<LfuNode<K, V>> node)
        {
            // there was a cache hit even if the item was removed or is not yet added.
            this.metrics.requestHitCount++;

            // Node is added to read buffer while it is removed by maintenance, or it is read before it has been added.
            if (node.List == null)
            {
                return;
            }

            switch (node.Value.Position)
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

        private void OnWrite(LinkedListNode<LfuNode<K, V>> node)
        {
            // Nodes can be removed while they are in the write buffer, in which case they should
            // not be added back into the LRU.
            if (node.Value.WasRemoved)
            {
                if (node.List != null)
                {
                    node.List.Remove(node);
                }

                return;
            }

            this.cmSketch.Increment(node.Value.Key);

            // node can already be in one of the queues due to update
            switch (node.Value.Position)
            {
                case Position.Window:
                    if (node.List == null)
                    {
                        this.windowLru.AddLast(node);
                        TryEvict();
                    }
                    else
                    {
                        this.windowLru.MoveToEnd(node);
                    }
                    break;
                case Position.Probation:
                    PromoteProbation(node);
                    break;
                case Position.Protected:
                    this.protectedLru.MoveToEnd(node);
                    break;
            }
        }

        private void TryEvict()
        {
            if (windowLru.Count > capacity.Window)
            {
                // move from window to probation
                var candidate = this.windowLru.First;
                this.windowLru.RemoveFirst();

                // initial state is empty protected, allow it to fill up before using probation
                if (this.protectedLru.Count < capacity.Protected)
                {
                    this.protectedLru.AddLast(candidate);
                    candidate.Value.Position = Position.Protected;
                    return;
                }

                this.probationLru.AddLast(candidate);
                candidate.Value.Position = Position.Probation;

                // remove either candidate or probation.first
                if (this.probationLru.Count > capacity.Probation)
                {
                    var c = this.cmSketch.EstimateFrequency(candidate.Value.Key);
                    var p = this.cmSketch.EstimateFrequency(this.probationLru.First.Value.Key);

                    // TODO: random factor?
                    var victim = (c > p) ? this.probationLru.First : candidate;

                    this.dictionary.TryRemove(victim.Value.Key, out var _);
                    victim.List.Remove(victim);

                    this.metrics.evictedCount++;
                }
            }
        }

        private void PromoteProbation(LinkedListNode<LfuNode<K, V>> node)
        {
            this.probationLru.Remove(node);
            this.protectedLru.AddLast(node);
            node.Value.Position = Position.Protected;

            if (this.protectedLru.Count > capacity.Protected)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();

                demoted.Value.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        [DebuggerDisplay("{Format()}")]
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
            private string Format()
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

        private class CacheMetrics : ICacheMetrics
        {
            public long requestHitCount;
            public long requestMissCount;
            public long evictedCount;

            public double HitRatio => (double)requestHitCount / (double)Total;

            public long Total => requestHitCount + requestMissCount;

            public long Hits => requestHitCount;

            public long Misses => requestMissCount;

            public long Evicted => evictedCount;
        }

#if DEBUG
        public string FormatLruString()
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
    }

    // Explicit layout cannot be a generic class member
    [StructLayout(LayoutKind.Explicit, Size = 2 * Padding.CACHE_LINE_SIZE)]
    internal struct PaddedInt
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public int Value;
    }
}
