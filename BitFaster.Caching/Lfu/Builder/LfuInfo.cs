﻿
using System;
using System.Collections.Generic;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu.Builder
{
    internal sealed class LfuInfo<K>
        where K : notnull
    {
        private object? expiry = null;

        public int Capacity { get; set; } = 128;

        public int ConcurrencyLevel { get; set; } = Defaults.ConcurrencyLevel;

        public IScheduler Scheduler { get; set; } = new ThreadPoolScheduler();

        public IEqualityComparer<K> KeyComparer { get; set; } = EqualityComparer<K>.Default;

        public TimeSpan? TimeToExpireAfterWrite { get; set; } = null;

        public TimeSpan? TimeToExpireAfterAccess { get; set; } = null;

        public void SetExpiry<V>(IExpiryCalculator<K, V> expiry) => this.expiry = expiry;

        public IExpiryCalculator<K, V>? GetExpiry<V>()
        {
            if (this.expiry == null)
            {
                return null;
            }

            var e = this.expiry as IExpiryCalculator<K, V>;

            if (e == null)
                Throw.InvalidOp($"Incompatible IExpiryCalculator value generic type argument, expected {typeof(IExpiryCalculator<K, V>)} but found {this.expiry.GetType()}");

            return e;
        }

        internal void ThrowIfExpirySpecified(string extensionName)
        {
            if (this.expiry != null)
                Throw.InvalidOp("WithExpireAfter is not compatible with " + extensionName);
        }
    }
}
