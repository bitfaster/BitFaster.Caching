using System;                                               

namespace BitFaster.Caching.UnitTests
{
    /// <summary>
    /// Defines a mechanism to determine the time to live for a cache item using function delegates.
    /// </summary>
    public class TestExpiryCalculator<K, V> : IExpiryCalculator<K, V>
    {
        private readonly Func<K, V, TimeSpan> expireAfterCreate;
        private readonly Func<K, V, TimeSpan, TimeSpan> expireAfterRead;
        private readonly Func<K, V, TimeSpan, TimeSpan> expireAfterUpdate;

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfter">The delegate that computes the item time to expire.</param>
        public TestExpiryCalculator(Func<K, V, TimeSpan> expireAfter)
        {
            this.expireAfterCreate = expireAfter;
            this.expireAfterRead = null;
            this.expireAfterUpdate = null;
        }

        /// <summary>
        /// Initializes a new instance of the Expiry class.
        /// </summary>
        /// <param name="expireAfterCreate">The delegate that computes the item time to expire at creation.</param>
        /// <param name="expireAfterRead">The delegate that computes the item time to expire after a read operation.</param>
        /// <param name="expireAfterUpdate">The delegate that computes the item time to expire after an update operation.</param>
        public TestExpiryCalculator(Func<K, V, TimeSpan> expireAfterCreate, Func<K, V, TimeSpan, TimeSpan> expireAfterRead, Func<K, V, TimeSpan, TimeSpan> expireAfterUpdate)
        {
            this.expireAfterCreate = expireAfterCreate;
            this.expireAfterRead = expireAfterRead;
            this.expireAfterUpdate = expireAfterUpdate;
        }

        ///<inheritdoc/>
        public TimeSpan GetExpireAfterCreate(K key, V value)
        {
            return this.expireAfterCreate(key, value);
        }

        ///<inheritdoc/>
        public TimeSpan GetExpireAfterRead(K key, V value, TimeSpan currentTtl)
        {
            return this.expireAfterRead == null ? this.expireAfterCreate(key, value) : this.expireAfterRead(key, value, currentTtl);
        }

        ///<inheritdoc/>
        public TimeSpan GetExpireAfterUpdate(K key, V value, TimeSpan currentTtl)
        {
            return this.expireAfterUpdate == null ? this.expireAfterCreate(key, value) : this.expireAfterUpdate(key, value, currentTtl);
        }
    }
}
