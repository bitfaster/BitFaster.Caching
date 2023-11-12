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
    public readonly struct AfterReadWriteLongTicksPolicy<K, V> : IItemPolicy<K, V, LongTickCountReadWriteLruItem<K, V>>
    {
        private readonly long readTimeToLive;
        private readonly long writeTimeToLive;
        private readonly Time time;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => StopwatchTickConverter.FromTicks(readTimeToLive);

        /// <summary>
        /// Gets the read time to live defined by the read policy.
        /// </summary>
        public TimeSpan ReadTimeToLive => TimeSpan.FromMilliseconds(readTimeToLive);

        /// <summary>
        /// Initializes a new instance of the AfterReadWriteLongTicksPolicy class with the specified time to live.
        /// </summary>
        /// <param name="readTimeToLive">The read time to live.</param>
        /// <param name="writeTimeToLive">The write time to live.</param>
        public AfterReadWriteLongTicksPolicy(TimeSpan readTimeToLive, TimeSpan writeTimeToLive)
        {
            this.readTimeToLive = StopwatchTickConverter.ToTicks(readTimeToLive);
            this.writeTimeToLive = StopwatchTickConverter.ToTicks(writeTimeToLive);
            this.time = new Time();
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
            item.ReadTickCount = this.time.Last;
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
            this.time.Last = Stopwatch.GetTimestamp();
            if (this.time.Last - item.ReadTickCount > this.readTimeToLive)
            {
                return true;
            }

            if (this.time.Last - item.WriteTickCount > this.writeTimeToLive)
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
    }
#endif
}
