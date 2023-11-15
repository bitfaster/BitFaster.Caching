using System;                                               

namespace BitFaster.Caching.UnitTests
{
    /// <summary>
    /// Defines a mechanism to determine the time to live for a cache item using function delegates.
    /// </summary>
    public class TestExpiryCalculator<K, V> : IExpiryCalculator<K, V>
    {
        public static readonly TimeSpan DefaultTimeToExpire = TimeSpan.FromMilliseconds(10);

        public Func<K, V, TimeSpan> ExpireAfterCreate { get; set; }
        public Func<K, V, TimeSpan, TimeSpan> ExpireAfterRead { get; set; }
        public Func<K, V, TimeSpan, TimeSpan> ExpireAfterUpdate { get; set; }

        public TestExpiryCalculator()
        {
            ExpireAfterCreate = (_, _) => DefaultTimeToExpire;
        }

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfter">The delegate that computes the item time to expire.</param>
        public TestExpiryCalculator(Func<K, V, TimeSpan> expireAfter)
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
        public TestExpiryCalculator(Func<K, V, TimeSpan> expireAfterCreate, Func<K, V, TimeSpan, TimeSpan> expireAfterRead, Func<K, V, TimeSpan, TimeSpan> expireAfterUpdate)
        {
            this.ExpireAfterCreate = expireAfterCreate;
            this.ExpireAfterRead = expireAfterRead;
            this.ExpireAfterUpdate = expireAfterUpdate;
        }

        ///<inheritdoc/>
        public TimeSpan GetExpireAfterCreate(K key, V value)
        {
            return this.ExpireAfterCreate(key, value);
        }

        ///<inheritdoc/>
        public TimeSpan GetExpireAfterRead(K key, V value, TimeSpan currentTtl)
        {
            return this.ExpireAfterRead == null ? this.ExpireAfterCreate(key, value) : this.ExpireAfterRead(key, value, currentTtl);
        }

        ///<inheritdoc/>
        public TimeSpan GetExpireAfterUpdate(K key, V value, TimeSpan currentTtl)
        {
            return this.ExpireAfterUpdate == null ? this.ExpireAfterCreate(key, value) : this.ExpireAfterUpdate(key, value, currentTtl);
        }
    }
}
