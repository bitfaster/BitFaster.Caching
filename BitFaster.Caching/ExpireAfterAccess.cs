using System;

namespace BitFaster.Caching
{
    internal class ExpireAfterAccess<K, V> : IExpiryCalculator<K, V>
    {
        private readonly Duration timeToExpire;

        public TimeSpan TimeToExpire => timeToExpire.ToTimeSpan();

        public ExpireAfterAccess(TimeSpan timeToExpire)
        {
            this.timeToExpire = Duration.FromTimeSpan(timeToExpire);
        }

        public Duration GetExpireAfterCreate(K key, V value)
        {
            return timeToExpire;
        }

        public Duration GetExpireAfterRead(K key, V value, Duration current)
        {
            return timeToExpire;
        }

        public Duration GetExpireAfterUpdate(K key, V value, Duration current)
        {
            return timeToExpire;
        }
    }
}
