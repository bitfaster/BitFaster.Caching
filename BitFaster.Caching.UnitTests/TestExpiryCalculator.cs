using System;                                               

namespace BitFaster.Caching.UnitTests
{
    /// <summary>
    /// Defines a mechanism to determine the time to live for a cache item using function delegates.
    /// </summary>
    public class TestExpiryCalculator<K, V> : IExpiryCalculator<K, V>
    {
        public static readonly Duration DefaultTimeToExpire = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(10));

        public Func<K, V, Duration> ExpireAfterCreate { get; set; }
        public Func<K, V, Duration, Duration> ExpireAfterRead { get; set; }
        public Func<K, V, Duration, Duration> ExpireAfterUpdate { get; set; }

        public TestExpiryCalculator()
        {
            ExpireAfterCreate = (_, _) => DefaultTimeToExpire;
        }

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfter">The delegate that computes the item time to expire.</param>
        public TestExpiryCalculator(Func<K, V, Duration> expireAfter)
        {
            this.ExpireAfterCreate = expireAfter;
            this.ExpireAfterRead = null;
            this.ExpireAfterUpdate = null;
        }

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfterCreate">The delegate that computes the item time to expire at creation.</param>
        /// <param name="expireAfterRead">The delegate that computes the item time to expire after a read operation.</param>
        /// <param name="expireAfterUpdate">The delegate that computes the item time to expire after an update operation.</param>
        public TestExpiryCalculator(Func<K, V, Duration> expireAfterCreate, Func<K, V, Duration, Duration> expireAfterRead, Func<K, V, Duration, Duration> expireAfterUpdate)
        {
            this.ExpireAfterCreate = expireAfterCreate;
            this.ExpireAfterRead = expireAfterRead;
            this.ExpireAfterUpdate = expireAfterUpdate;
        }

        ///<inheritdoc/>
        public Duration GetExpireAfterCreate(K key, V value)
        {
            return this.ExpireAfterCreate(key, value);
        }

        ///<inheritdoc/>
        public Duration GetExpireAfterRead(K key, V value, Duration currentTtl)
        {
            return this.ExpireAfterRead == null ? this.ExpireAfterCreate(key, value) : this.ExpireAfterRead(key, value, currentTtl);
        }

        ///<inheritdoc/>
        public Duration GetExpireAfterUpdate(K key, V value, Duration currentTtl)
        {
            return this.ExpireAfterUpdate == null ? this.ExpireAfterCreate(key, value) : this.ExpireAfterUpdate(key, value, currentTtl);
        }
    }
}
