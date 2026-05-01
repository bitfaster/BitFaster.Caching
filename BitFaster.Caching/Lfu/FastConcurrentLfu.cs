using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
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
    [DebuggerTypeProxy(typeof(FastConcurrentLfu<,,,>.LfuDebugView<,>))]
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    internal sealed class FastConcurrentLfu<K, V, N, P> : ICacheExt<K, V>, IAsyncCacheExt<K, V>, IBoundedPolicy
        where K : notnull
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N, NoEventPolicy<K,V>>
    {
        // Note: for performance reasons this is a mutable struct, it cannot be readonly.
        private ConcurrentLfuCore<K, V, N, P, NoEventPolicy<K, V>> core;

        /// <summary>
        /// The default buffer size.
        /// </summary>
        public const int DefaultBufferSize = 128;

        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified concurrencyLevel, capacity, scheduler, equality comparer and buffer size.
        /// </summary>
        /// <param name="concurrencyLevel">The concurrency level.</param>
        /// <param name="capacity">The capacity.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <param name="comparer">The equality comparer.</param>
        public FastConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer)
        {
            NoEventPolicy<K, V> eventPolicy = default;
            eventPolicy.SetEventSource(this);
            this.core = new(concurrencyLevel, capacity, scheduler, comparer, () => this.DrainBuffers(), default, eventPolicy);
        }

        public FastConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, P nodePolicy)
        {
            NoEventPolicy<K, V> eventPolicy = default;
            eventPolicy.SetEventSource(this);
            this.core = new(concurrencyLevel, capacity, scheduler, comparer, () => this.DrainBuffers(), nodePolicy, eventPolicy);
        }

        internal ConcurrentLfuCore<K, V, N, P, NoEventPolicy<K, V>> Core => core;

        // structs cannot declare self referencing lambda functions, therefore pass this in from the ctor
        private void DrainBuffers()
        {
            this.core.DrainBuffers();
        }

        ///<inheritdoc/>
        public int Count => core.Count;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => core.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => new(new Proxy(this));

        internal ref NoEventPolicy<K, V> EventPolicyRef => ref this.core.eventPolicy;

        ///<inheritdoc/>
        public CachePolicy Policy => core.Policy;

        ///<inheritdoc/>
        public ICollection<K> Keys => core.Keys;

#if NET9_0_OR_GREATER
        /// <inheritdoc/>
        public IEqualityComparer<K> Comparer => this.core.Comparer;
#endif

        ///<inheritdoc/>
        public int Capacity => core.Capacity;

        ///<inheritdoc/>
        public IScheduler Scheduler => core.Scheduler;

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
            core.DoMaintenance();
        }

        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            core.AddOrUpdate(key, value);
        }

        ///<inheritdoc/>
        public void Clear()
        {
            core.Clear();
        }

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            return core.GetOrAdd(key, valueFactory);
        }

        ///<inheritdoc/>
        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
#if NET9_0_OR_GREATER
            where TArg : allows ref struct
#endif
        {
            return core.GetOrAdd(key, valueFactory, factoryArgument);
        }

        ///<inheritdoc/>
        public ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            return core.GetOrAddAsync(key, valueFactory);
        }

        ///<inheritdoc/>
        public ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            return core.GetOrAddAsync(key, valueFactory, factoryArgument);
        }

        ///<inheritdoc/>
        public void Trim(int itemCount)
        {
            core.Trim(itemCount);
        }

        ///<inheritdoc/>
        public bool TryGet(K key, [MaybeNullWhen(false)] out V value)
        {
            return core.TryGet(key, out value);
        }

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            return core.TryRemove(key);
        }

        /// <summary>
        /// Attempts to remove the specified key value pair.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>true if the item was removed successfully; otherwise, false.</returns>
        public bool TryRemove(KeyValuePair<K, V> item)
        {
            return core.TryRemove(item);
        }

        /// <summary>
        /// Attempts to remove and return the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed, or the default value of the value type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public bool TryRemove(K key, [MaybeNullWhen(false)] out V value)
        {
            return core.TryRemove(key, out value);
        }

        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            return core.TryUpdate(key, value);
        }

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return core.GetEnumerator();
        }

        ///<inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return core.GetEnumerator();
        }

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAlternateLookup<TAlternateKey, K, V> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            return core.GetAlternateLookup<TAlternateKey>();
        }

        ///<inheritdoc/>
        public bool TryGetAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            return core.TryGetAlternateLookup(out lookup);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncAlternateLookup<TAlternateKey, K, V> GetAsyncAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            return core.GetAsyncAlternateLookup<TAlternateKey>();
        }

        ///<inheritdoc/>
        public bool TryGetAsyncAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAsyncAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            return core.TryGetAsyncAlternateLookup(out lookup);
        }
#endif

#if DEBUG
        /// <summary>
        /// Format the LFU as a string by converting all the keys to strings.
        /// </summary>
        /// <returns>The LFU formatted as a string.</returns>
        public string FormatLfuString()
        {
            return core.FormatLfuString();
        }
#endif

        // To get JIT optimizations, policies must be structs.
        // If the structs are returned directly via properties, they will be copied. Since
        // eventPolicy is a mutable struct, copy is bad since changes are lost.
        // Hence it is returned by ref and mutated via Proxy.
        private class Proxy : ICacheEvents<K, V>
        {
            private readonly FastConcurrentLfu<K, V, N, P> lfu;

            public Proxy(FastConcurrentLfu<K, V, N, P> lfu)
            {
                this.lfu = lfu;
            }

            public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                add
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemRemoved += value;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                remove
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemRemoved -= value;
                }
            }

            // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
            public event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                add
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemUpdated += value;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                remove
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemUpdated -= value;
                }
            }
#endif
        }

        [ExcludeFromCodeCoverage]
        internal class LfuDebugView<N, P>
             where N : LfuNode<K, V>
             where P : struct, INodePolicy<K, V, N, NoEventPolicy<K, V>>
        {
            private readonly FastConcurrentLfu<K, V, N, P> lfu;

            public LfuDebugView(FastConcurrentLfu<K, V, N, P> lfu)
            {
                this.lfu = lfu;
            }

            public string Maintenance => lfu.core.drainStatus.Format();

            public ICacheMetrics? Metrics => lfu.Metrics.Value;

            public StripedMpscBuffer<N> ReadBuffer => (this.lfu.core.readBuffer as StripedMpscBuffer<N>)!;

            public MpscBoundedBuffer<N> WriteBuffer => (this.lfu.core.writeBuffer as MpscBoundedBuffer<N>)!;

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
}
