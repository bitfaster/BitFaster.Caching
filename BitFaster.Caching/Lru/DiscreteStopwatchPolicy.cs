using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
#if !NETCOREAPP3_0_OR_GREATER
    internal readonly struct DiscretePolicy<K, V> : IDiscreteItemPolicy<K, V>
    {
        private readonly IExpiryCalculator<K, V> expiry;
        private readonly Time time;

        public TimeSpan TimeToLive => TimeSpan.Zero;

        ///<inheritdoc/>
        public TimeSpan ConvertTicks(long ticks) => StopwatchTickConverter.FromTicks(ticks - Stopwatch.GetTimestamp());

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
            return new LongTickCountLruItem<K, V>(key, value, expiry.raw + Stopwatch.GetTimestamp());
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            var currentExpiry = item.TickCount - this.time.Last;
            var newExpiry = expiry.GetExpireAfterRead(item.Key, item.Value, new Interval(currentExpiry));
            item.TickCount = this.time.Last + newExpiry.raw;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            var time = Stopwatch.GetTimestamp();
            var currentExpiry = item.TickCount - time;
            var newExpiry = expiry.GetExpireAfterUpdate(item.Key, item.Value, new Interval(currentExpiry));
            item.TickCount = time + newExpiry.raw;
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
