using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
#if !NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Implement an expire after access policy.
    /// </summary>    
    /// <remarks>
    /// This class measures time using Stopwatch.GetTimestamp() with a resolution of ~1us.
    /// </remarks>
    internal readonly struct AfterAccessLongTicksPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        private readonly long timeToLive;
        private readonly Time time;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => StopwatchTickConverter.FromTicks(timeToLive);

        /// <summary>
        /// Initializes a new instance of the TLruLongTicksPolicy class with the specified time to live.
        /// </summary>
        /// <param name="timeToLive">The time to live.</param>
        public AfterAccessLongTicksPolicy(TimeSpan timeToLive)
        {
            this.timeToLive = StopwatchTickConverter.ToTicks(timeToLive);
            this.time = new Time();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            return new LongTickCountLruItem<K, V>(key, value, Stopwatch.GetTimestamp());
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            item.TickCount = this.time.Last;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            item.TickCount = Stopwatch.GetTimestamp();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            this.time.Last = Stopwatch.GetTimestamp();
            if (this.time.Last - item.TickCount > this.timeToLive)
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
