/*
 * Copyright 2015 Ben Manes. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// An LFU cache with a W-TinyLfu eviction policy.
    /// </summary>
    /// <remarks>
    /// Based on Caffeine BoundedLocalCache:
    /// https://github.com/ben-manes/caffeine/blob/master/caffeine/src/main/java/com/github/benmanes/caffeine/cache/BoundedLocalCache.java
    /// </remarks>
    public class ConcurrentLfu<K, V> : ICache<K, V>
    {
        private readonly ConcurrentDictionary<K, LinkedListNode<LfuNode<K, V>>> dictionary;

        private readonly ConcurrentQueue<LinkedListNode<LfuNode<K, V>>> readBuffer;
        private readonly ConcurrentQueue<LinkedListNode<LfuNode<K, V>>> writeBuffer;

        private readonly CacheMetrics metrics = new CacheMetrics();

        private readonly CmSketch<K> cmSketch;

        private readonly LinkedList<LfuNode<K, V>> windowLru;
        private readonly LinkedList<LfuNode<K, V>> probationLru;
        private readonly LinkedList<LfuNode<K, V>> protectedLru;

        private int windowMax;
        private int protectedMax;
        private int probationMax;

        private readonly DrainStatus drainStatus = new DrainStatus();
        private readonly object maintenanceLock = new object();

        public ConcurrentLfu(int capacity)
        {
            var comparer = EqualityComparer<K>.Default;
            this.dictionary = new ConcurrentDictionary<K, LinkedListNode<LfuNode<K, V>>>(Defaults.ConcurrencyLevel, capacity, comparer);
            this.readBuffer = new ConcurrentQueue<LinkedListNode<LfuNode<K, V>>>();
            this.writeBuffer = new ConcurrentQueue<LinkedListNode<LfuNode<K, V>>>();
            this.cmSketch = new CmSketch<K>(1, comparer);
            this.cmSketch.EnsureCapacity(capacity);
            this.windowLru = new LinkedList<LfuNode<K, V>>();
            this.probationLru = new LinkedList<LfuNode<K, V>>();
            this.protectedLru = new LinkedList<LfuNode<K, V>>();

            // this is not correct but easy way to get started:
            var partition = new FavorWarmPartition(capacity);
            this.windowMax = partition.Hot;
            this.protectedMax = partition.Warm;
            this.probationMax = partition.Cold;
        }

        public int Count => this.dictionary.Count;

        public Optional<ICacheMetrics> Metrics => new Optional<ICacheMetrics>(this.metrics);

        public Optional<ICacheEvents<K, V>> Events => Optional<ICacheEvents<K, V>>.None();

        public CachePolicy Policy => throw new NotImplementedException();

        public ICollection<K> Keys => this.dictionary.Keys;

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
                    this.writeBuffer.Enqueue(node);
                    return;
                }
            }
        }

        public void Clear()
        {
            lock (maintenanceLock)
            {
                // TODO: is this correct? and also Trim - much like Caffeine void evictFromMain(int candidates)

#if NETSTANDARD2_0
                while (this.readBuffer.TryDequeue(out var _))
                {
                }

                while (this.writeBuffer.TryDequeue(out var _))
                {
                }
#else
                this.readBuffer.Clear();
                this.writeBuffer.Clear();
#endif

                this.windowLru.Clear();
                this.probationLru.Clear();
                this.protectedLru.Clear();

                this.cmSketch.Clear();
                this.dictionary.Clear();
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
                    this.writeBuffer.Enqueue(node);
                    AfterWrite();
                    return node.Value.Value;
                }
            }
        }

        public bool TryGet(K key, out V value)
        {
            // TODO: should this be counted as a read in CMSketch? how to enque with no node?

            if (this.dictionary.TryGetValue(key, out var node))
            {
                this.readBuffer.Enqueue(node);
                TryScheduleDrain();
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
                this.writeBuffer.Enqueue(node);
                TryScheduleDrain();
                return true;
            }

            return false;
        }

        public bool TryUpdate(K key, V value)
        {
            if (this.dictionary.TryGetValue(key, out var node))
            {
                node.Value.Value = value;
                this.writeBuffer.Enqueue(node);
                TryScheduleDrain();
                return true;
            }

            return false;
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.dictionary)
            {
                yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.Value.Value);
            }
        }

        private void AfterWrite()
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
                        if (this.drainStatus.Cas(DrainStatus.ProcessingToIdle, DrainStatus.ProcessingToRequired) == DrainStatus.ProcessingToIdle)
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
                    Task.Run(() => DrainBuffers());
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
            lock (maintenanceLock)
            {
                Maintenance();
            }

            if (this.drainStatus.Status() >= DrainStatus.Required)
            {
                DrainBuffers();
            }
        }

        private void Maintenance()
        {
            this.drainStatus.Set(DrainStatus.ProcessingToIdle);

            // It's possible to get stuck here forever if incoming rate is high
            // In a tight loop (like the benchmark) we will currently accumulate tens of thousands of items
            // in the read buffer. In this case probably better to discard new reads rather than
            // accumulating a massive buffer that cannot drain.
            // This would mean that dictionary contains nodes that are not in the LRU list, however, which is bad.
            // https://github.com/dotnet/runtime/issues/23700
            while (this.readBuffer.TryDequeue(out var node))
            {
                OnAccess(node);
            }

            while (this.writeBuffer.TryDequeue(out var node))
            {
                OnWrite(node);
            }

            // TODO: evict entries?
            // TODO: climb

            if (this.drainStatus.Cas(DrainStatus.ProcessingToIdle, DrainStatus.Idle) != DrainStatus.ProcessingToIdle)
            {
                this.drainStatus.Set(DrainStatus.Required);
            }
        }

        private void OnAccess(LinkedListNode<LfuNode<K, V>> node)
        {
            this.cmSketch.Increment(node.Value.Key);

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

            this.metrics.requestHitCount++;
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
            if (windowLru.Count > windowMax)
            {
                // move from window to probation
                var candidate = this.windowLru.First;
                this.windowLru.RemoveFirst();

                // initial state is empty protected, allow it to fill up before using probation
                if (this.protectedLru.Count < protectedMax)
                {
                    this.protectedLru.AddLast(candidate);
                    candidate.Value.Position = Position.Protected;
                    return;
                }

                this.probationLru.AddLast(candidate);
                candidate.Value.Position = Position.Probation;

                // remove either candidate or probation.first
                if (this.probationLru.Count > probationMax)
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

            if (this.protectedLru.Count > protectedMax)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();

                demoted.Value.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        // padding is about 50% faster in getoradd bench
        //private class PadDrainStatus
        //{
        //    byte p000, p001, p002, p003, p004, p005, p006, p007;
        //    byte p008, p009, p010, p011, p012, p013, p014, p015;
        //    byte p016, p017, p018, p019, p020, p021, p022, p023;
        //    byte p024, p025, p026, p027, p028, p029, p030, p031;
        //    byte p032, p033, p034, p035, p036, p037, p038, p039;
        //    byte p040, p041, p042, p043, p044, p045, p046, p047;
        //    byte p048, p049, p050, p051, p052, p053, p054, p055;
        //    byte p056, p057, p058, p059, p060, p061, p062, p063;
        //    byte p064, p065, p066, p067, p068, p069, p070, p071;
        //    byte p072, p073, p074, p075, p076, p077, p078, p079;
        //    byte p080, p081, p082, p083, p084, p085, p086, p087;
        //    byte p088, p089, p090, p091, p092, p093, p094, p095;
        //    byte p096, p097, p098, p099, p100, p101, p102, p103;
        //    byte p104, p105, p106, p107, p108, p109, p110, p111;
        //    byte p112, p113, p114, p115, p116, p117, p118, p119;
        //}

        // TODO: investigate false sharing in detail. See PaddedHeadAndTail
        // https://github.com/dotnet/corefx/blob/9c468a08151402a68732c784b0502437b808df9f/src/System.Collections.Concurrent/src/System/Collections/Concurrent/ConcurrentQueue.cs
        [DebuggerDisplay("{Format()}")]
        private class DrainStatus //: PadDrainStatus
        {
            public const int Idle = 0;
            public const int Required = 1;
            public const int ProcessingToIdle = 2;
            public const int ProcessingToRequired = 3;

            private PaddedInt drainStatus; // mutable struct, don't mark readonly

            public bool ShouldDrain(bool delayable)
            {
                switch (this.drainStatus.Value)
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
                this.drainStatus.Value = newStatus; 
            }

            public int Cas(int oldStatus, int newStatus)
            { 
                return Interlocked.CompareExchange(ref this.drainStatus.Value, newStatus, oldStatus);
            }

            public int Status()
            {
                return this.drainStatus.Value;
            }

            public string Format()
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
    }

    // Explicit layout cannot be a generic class member
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct PaddedInt
    {
        [FieldOffset(128)] public volatile int Value;
    }
}
