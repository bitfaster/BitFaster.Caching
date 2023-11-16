﻿using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// Defines a mechanism to calculate when cache entries expire based on the item key, value 
    /// or existing time to expire.
    /// </summary>
    public interface IExpiryCalculator<K, V>
    {
        /// <summary>
        /// Specify the inital time to expire after an entry is created.
        /// </summary>
        Duration GetExpireAfterCreate(K key, V value);

        /// <summary>
        /// Specify the time to expire after an entry is read. The current time to expire may be
        /// be returned to not modify the expiration time.
        /// </summary>
        Duration GetExpireAfterRead(K key, V value, Duration current);

        /// <summary>
        /// Specify the time to expire after an entry is updated.The current time to expire may be
        /// be returned to not modify the expiration time.
        /// </summary>
        Duration GetExpireAfterUpdate(K key, V value, Duration current);
    }
}
