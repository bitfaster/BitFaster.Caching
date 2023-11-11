using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
#if NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Implement an expire after read and expire after write policy.
    /// </summary>
    /// <remarks>
    /// This class measures time using Environment.TickCount64, which is significantly faster
    /// than both Stopwatch.GetTimestamp and DateTime.UtcNow. However, resolution is lower (typically 
    /// between 10-16ms), vs 1us for Stopwatch.GetTimestamp.
    /// </remarks>
    public readonly struct AfterReadWriteTickCount64Policy<K, V> : IItemPolicy<K, V, LongTickCountReadWriteLruItem<K, V>>
    {
        private readonly long readTimeToLive;
        private readonly long writeTimeToLive;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => TimeSpan.FromMilliseconds(readTimeToLive);

        /// <summary>
        /// Initializes a new instance of the AfterReadTickCount64Policy class with the specified time to live.
        /// </summary>
        /// <param name="readTimeToLive">The read time to live.</param>
        /// <param name="writeTimeToLive">The write time to live.</param>
        public AfterReadWriteTickCount64Policy(TimeSpan readTimeToLive, TimeSpan writeTimeToLive)
        {
            TimeSpan maxRepresentable = TimeSpan.FromTicks(9223372036854769664);
            if (readTimeToLive <= TimeSpan.Zero || readTimeToLive > maxRepresentable)
                Throw.ArgOutOfRange(nameof(readTimeToLive), $"Value must greater than zero and less than {maxRepresentable}");

            if (writeTimeToLive <= TimeSpan.Zero || writeTimeToLive > maxRepresentable)
                Throw.ArgOutOfRange(nameof(readTimeToLive), $"Value must greater than zero and less than {maxRepresentable}");

            this.readTimeToLive = (long)readTimeToLive.TotalMilliseconds;
            this.writeTimeToLive = (long)writeTimeToLive.TotalMilliseconds;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountReadWriteLruItem<K, V> CreateItem(K key, V value)
        {
            return new LongTickCountReadWriteLruItem<K, V>(key, value, Environment.TickCount64);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountReadWriteLruItem<K, V> item)
        {
            item.ReadTickCount = Environment.TickCount64;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountReadWriteLruItem<K, V> item)
        {
            item.WriteTickCount = Environment.TickCount64;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountReadWriteLruItem<K, V> item)
        {
            var tc = Environment.TickCount64;
            if (tc - item.ReadTickCount > this.readTimeToLive)
            {
                return true;
            }

            if (tc - item.WriteTickCount > this.writeTimeToLive)
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
