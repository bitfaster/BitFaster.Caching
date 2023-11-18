using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    internal readonly struct DiscreteItemPolicy<K, V> : IDiscreteItemPolicy<K, V>
    {
        private readonly IExpiryCalculator<K, V> expiry;
        private readonly Time time;

        public TimeSpan TimeToLive => TimeSpan.Zero;

        public DiscreteItemPolicy(IExpiryCalculator<K, V> expiry)
        {
            if (expiry == null)
                Throw.ArgNull(ExceptionArgument.expiry);

            this.expiry = expiry;
            this.time = new Time();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            var expiry = this.expiry.GetExpireAfterCreate(key, value);
            return new LongTickCountLruItem<K, V>(key, value, (expiry + Duration.SinceEpoch()).raw);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            var currentExpiry = new Duration(item.TickCount - this.time.Last);
            var newExpiry = expiry.GetExpireAfterRead(item.Key, item.Value, currentExpiry);
            item.TickCount = this.time.Last + newExpiry.raw;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            var time = Duration.SinceEpoch();
            var currentExpiry = new Duration(item.TickCount) - time;
            var newExpiry = expiry.GetExpireAfterUpdate(item.Key, item.Value, currentExpiry);
            item.TickCount = (time + newExpiry).raw;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            this.time.Last = Duration.SinceEpoch().raw;
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
