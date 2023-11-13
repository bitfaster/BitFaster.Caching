using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Defines a mechanism to calculate when cache entries expire.
    /// </summary>
    public interface IExpiry<K, V>
    {
        /// <summary>
        /// Specify the inital time to live after an entry is created.
        /// </summary>
        TimeSpan GetExpireAfterCreate(K key, V value);

        /// <summary>
        /// Specify the time to live after an entry is read.
        /// </summary>
        TimeSpan GetExpireAfterRead(K key, V value);

        /// <summary>
        /// Specify the time to live after an entry is updated.
        /// </summary>
        TimeSpan GetExpireAfterUpdate(K key, V value);
    }

    /// <summary>
    /// Defines a mechanism to determine the time to live for a cache item using function delegates.
    /// </summary>
    public readonly struct Expiry<K, V> : IExpiry<K, V>
    {
        private readonly Func<K, V, TimeSpan> expireAfterCreate;
        private readonly Func<K, V, TimeSpan> expireAfterRead;
        private readonly Func<K, V, TimeSpan> expireAfterUpdate;

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfter">The delegate that computes the item time to live.</param>
        public Expiry(Func<K, V, TimeSpan> expireAfter)
        {
            this.expireAfterCreate = expireAfter;
            this.expireAfterRead = expireAfter;
            this.expireAfterUpdate = expireAfter;
        }

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfterCreate">The delegate that computes the item time to live at creation.</param>
        /// <param name="expireAfterRead">The delegate that computes the item time to live after a read operation.</param>
        /// <param name="expireAfterUpdate">The delegate that computes the item time to live after an update operation.</param>
        public Expiry(Func<K, V, TimeSpan> expireAfterCreate, Func<K, V, TimeSpan> expireAfterRead, Func<K, V, TimeSpan> expireAfterUpdate)
        {
            this.expireAfterCreate = expireAfterCreate;
            this.expireAfterRead = expireAfterRead;
            this.expireAfterUpdate = expireAfterUpdate;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan GetExpireAfterCreate(K key, V value)
        {
            return this.expireAfterCreate(key, value);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan GetExpireAfterRead(K key, V value)
        {
            return this.expireAfterRead(key, value);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan GetExpireAfterUpdate(K key, V value)
        {
            return this.expireAfterUpdate(key, value);
        }
    }

#if NETCOREAPP3_0_OR_GREATER
    internal readonly struct CustomExpiryPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        private readonly IExpiry<K, V> expiry;
        private readonly Time time;

        public TimeSpan TimeToLive => TimeSpan.Zero;

        public CustomExpiryPolicy(IExpiry<K, V> expiry)
        {
            this.expiry = expiry;
            this.time = new Time();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            var ttl = expiry.GetExpireAfterCreate(key, value);
            return new LongTickCountLruItem<K, V>(key, value, ttl.Ticks + Environment.TickCount64);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            var ttl = expiry.GetExpireAfterRead(item.Key, item.Value);
            item.TickCount = this.time.Last + ttl.Ticks;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            var ttl = expiry.GetExpireAfterUpdate(item.Key, item.Value);
            item.TickCount = Environment.TickCount64 + ttl.Ticks;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            this.time.Last = Environment.TickCount64;
            if (this.time.Last > item.TickCount)
            {
                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanDiscard()
        {
            return true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteWarm(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteCold(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Remove;
        }
    }
#else
    internal readonly struct CustomExpiryPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        private readonly IExpiry<K, V> expiry;
        private readonly Time time;

        public TimeSpan TimeToLive => TimeSpan.Zero;

        public CustomExpiryPolicy(IExpiry<K, V> expiry)
        {
            this.expiry = expiry;
            this.time = new Time();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            var ttl = expiry.GetExpireAfterCreate(key, value);
            return new LongTickCountLruItem<K, V>(key, value, StopwatchTickConverter.ToTicks(ttl) + Stopwatch.GetTimestamp());
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            var ttl = expiry.GetExpireAfterRead(item.Key, item.Value);
            item.TickCount = this.time.Last + StopwatchTickConverter.ToTicks(ttl);
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            var ttl = expiry.GetExpireAfterUpdate(item.Key, item.Value);
            item.TickCount = Stopwatch.GetTimestamp() + StopwatchTickConverter.ToTicks(ttl);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            this.time.Last = Stopwatch.GetTimestamp();
            if (this.time.Last > item.TickCount)
            {
                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanDiscard()
        {
            return true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteWarm(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteCold(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Remove;
        }
    }

#endif
}
