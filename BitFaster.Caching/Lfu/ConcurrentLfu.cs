using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Lfu
{
    // https://github.com/ben-manes/caffeine/blob/master/caffeine/src/main/java/com/github/benmanes/caffeine/cache/BoundedLocalCache.java
    // TODO: dispose
    // TODO: metrics
    // TODO: events?
    // TODO: policy
    public class ConcurrentLfu<K, V> : ICache<K, V>
    {
        private ConcurrentDictionary<K, LinkedListNode<LfuNode<K, V>>> dictionary;

        private ConcurrentQueue<LinkedListNode<LfuNode<K, V>>> readBuffer;
        private ConcurrentQueue<LinkedListNode<LfuNode<K, V>>> writeBuffer;

        private readonly CacheMetrics metrics = new CacheMetrics();

        private CmSketch<K> cmSketch;

        private LinkedList<LfuNode<K, V>> windowLru;
        private LinkedList<LfuNode<K, V>> probationLru;
        private LinkedList<LfuNode<K, V>> protectedLru;

        private int windowMax;
        private int protectedMax;
        private int probationMax;

        private DrainStatus drainStatus = new DrainStatus();
        private object padLock = new object();

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

            var partition = new FavorWarmPartition(capacity);
            this.windowMax = partition.Hot;
            this.protectedMax = partition.Warm;
            this.probationMax = partition.Cold;
        }

        public int Count => this.dictionary.Count;

        public Optional<ICacheMetrics> Metrics => new Optional<ICacheMetrics>(this.metrics);

        public Optional<ICacheEvents<K, V>> Events => throw new NotImplementedException();

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
            // TODO: implement this and also Trim - much like  void evictFromMain(int candidates)
            // stop threads
            // clear stuff
            throw new NotImplementedException();
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
            TryScheduleDrain();
            Interlocked.Increment(ref this.metrics.requestTotalCount);

            // TODO: should this be counted as a read in CMSketch? how to enque with no node?

            if (this.dictionary.TryGetValue(key, out var node))
            {
                this.readBuffer.Enqueue(node);
                value = node.Value.Value;               
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRemove(K key)
        {
            TryScheduleDrain();

            if (this.dictionary.TryRemove(key, out var node))
            {
                // TODO: this is not thread safe - mark as removed so that drain removes it somehow?
                node.List.Remove(node);
                return true;
            }

            return false;
        }

        public bool TryUpdate(K key, V value)
        {
            TryScheduleDrain();

            if (this.dictionary.TryGetValue(key, out var node))
            {
                node.Value.Value = value;
                this.writeBuffer.Enqueue(node);
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
                Monitor.TryEnter(padLock, ref lockTaken);
                int status = this.drainStatus.Status();

                if (status >= DrainStatus.ProcessingToIdle)
                {
                    return;
                }

                this.drainStatus.Set(DrainStatus.ProcessingToIdle);
                Task.Run(() => DrainBuffers());
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(padLock);
                }
            }
        }

        private void DrainBuffers()
        {
            lock (padLock)
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

            switch (node.Value.Position)
            {
                case Position.Window:
                    // First time round write queue is not drained, and it is not yet in queue. In that case just wait
                    // for write drain to add it
                    if (node.List != null)
                    {
                        this.windowLru.MoveToEnd(node); 
                    }
                    break;
                case Position.Probation:
                    ReorderProbation(node);
                    break;
                case Position.Protected:
                    this.protectedLru.MoveToEnd(node);
                    break;
            }

            this.metrics.requestHitCount++;
        }

        private void ReorderProbation(LinkedListNode<LfuNode<K, V>> node)
        {
            // promote on read
            this.probationLru.Remove(node);
            this.protectedLru.AddLast(node);
            node.Value.Position = Position.Protected;
        }

        private void OnWrite(LinkedListNode<LfuNode<K, V>> node)
        {
            // TODO: increment miss when queue == window
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

                    // TODO: see  boolean admit(K candidateKey, K victimKey), has random factor to block attack
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

        // TODO: false sharing etc.
        private class DrainStatus
        {
            public const int Idle = 0;
            public const int Required = 1;
            public const int ProcessingToIdle = 2;
            public const int ProcessingToRequired = 3;

            private volatile int drainStatus;

            public bool ShouldDrain(bool delayable)
            {
                switch (this.drainStatus)
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
                this.drainStatus = newStatus; 
            }

            public int Cas(int oldStatus, int newStatus)
            { 
                return Interlocked.CompareExchange(ref this.drainStatus, newStatus, oldStatus);
            }

            public int Status()
            {
                return this.drainStatus;
            }
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
        }
    }
}
