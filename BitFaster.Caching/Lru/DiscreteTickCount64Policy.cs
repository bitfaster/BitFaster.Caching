using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    internal readonly struct DiscretePolicy<K, V> : IDiscreteItemPolicy<K, V>
    {
        private readonly IExpiryCalculator<K, V> expiry;
        private readonly Time time;

        public TimeSpan TimeToLive => TimeSpan.Zero;

        public DiscretePolicy(IExpiryCalculator<K, V> expiry)
        {
            this.expiry = expiry;
            this.time = new Time();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            var expiry = this.expiry.GetExpireAfterCreate(key, value);
            return new LongTickCountLruItem<K, V>(key, value, expiry + Duration.SinceEpoch());
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            var currentExpiry = item.TickCount - this.time.Last;
            var newExpiry = expiry.GetExpireAfterRead(item.Key, item.Value, currentExpiry);
            item.TickCount = this.time.Last + newExpiry;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            var time = Duration.SinceEpoch();
            var currentExpiry = item.TickCount - time;
            var newExpiry = expiry.GetExpireAfterUpdate(item.Key, item.Value, currentExpiry);
            item.TickCount = time + newExpiry;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            this.time.Last = Duration.SinceEpoch();
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
}
