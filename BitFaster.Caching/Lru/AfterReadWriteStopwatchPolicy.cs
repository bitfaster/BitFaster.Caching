using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
#if !NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Implement an expire after read and expire after write policy.
    /// </summary>    
    /// <remarks>
    /// This class measures time using Stopwatch.GetTimestamp() with a resolution of ~1us.
    /// </remarks>
    public readonly struct AfterReadWriteStopwatchPolicy<K, V> : IItemPolicy<K, V, LongTickCountReadWriteLruItem<K, V>>
    {
        private readonly long readTimeToLive;
        private readonly long writeTimeToLive;

        /// <summary>
        /// Initializes a new instance of the AfterReadWriteStopwatchPolicy class with the specified time to live.
        /// </summary>
        /// <param name="readTimeToLive">The read time to live.</param>
        /// <param name="writeTimeToLive">The write time to live.</param>
        public AfterReadWriteStopwatchPolicy(TimeSpan readTimeToLive, TimeSpan writeTimeToLive)
        {
            this.readTimeToLive = ToTicks(readTimeToLive);
            this.writeTimeToLive = ToTicks(writeTimeToLive);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountReadWriteLruItem<K, V> CreateItem(K key, V value)
        {
            return new LongTickCountReadWriteLruItem<K, V>(key, value, Stopwatch.GetTimestamp());
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountReadWriteLruItem<K, V> item)
        {
            item.ReadTickCount = Stopwatch.GetTimestamp();
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountReadWriteLruItem<K, V> item)
        {
            item.WriteTickCount = Stopwatch.GetTimestamp();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountReadWriteLruItem<K, V> item)
        {
            var ts = Stopwatch.GetTimestamp();
            if (ts - item.ReadTickCount > this.readTimeToLive)
            {
                return true;
            }

            if (ts - item.WriteTickCount > this.writeTimeToLive)
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
        public ItemDestination RouteHot(LongTickCountReadWriteLruItem<K, V> item)
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
        public ItemDestination RouteWarm(LongTickCountReadWriteLruItem<K, V> item)
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
        public ItemDestination RouteCold(LongTickCountReadWriteLruItem<K, V> item)
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

        ///<inheritdoc/>
        public TimeSpan TimeToLive => FromTicks(readTimeToLive);

        /// <summary>
        /// Convert from TimeSpan to ticks.
        /// </summary>
        /// <param name="timespan">The time represented as a TimeSpan.</param>
        /// <returns>The time represented as ticks.</returns>
        public static long ToTicks(TimeSpan timespan)
        {
            return StopwatchTickConverter.ToTicks(timespan);
        }

        /// <summary>
        /// Convert from ticks to a TimeSpan.
        /// </summary>
        /// <param name="ticks">The time represented as ticks.</param>
        /// <returns>The time represented as a TimeSpan.</returns>
        public static TimeSpan FromTicks(long ticks)
        {
            return StopwatchTickConverter.FromTicks(ticks);
        }
    }
#endif
}
