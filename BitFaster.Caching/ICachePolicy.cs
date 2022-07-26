using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public class CachePolicy
    {
        public CachePolicy(IBoundedPolicy eviction, ITimePolicy expireAfterWrite)
        {
            this.Eviction = eviction;
            this.ExpireAfterWrite = expireAfterWrite;
        }

        public IBoundedPolicy Eviction { get; }

        public ITimePolicy ExpireAfterWrite { get; }
    }

    public interface IBoundedPolicy
    {
        /// <summary>
        /// Gets the total number of items that can be stored in the cache.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Trim the specified number of items from the cache.
        /// </summary>
        /// <param name="itemCount">The number of items to remove.</param>
        void Trim(int itemCount);
    }

    public interface ITimePolicy
    {
        /// <summary>
        /// Gets a value indicating whether the cache can expire items based on time.
        /// </summary>
        bool CanExpire { get; }

        /// <summary>
        /// Gets the time to live for items in the cache.
        /// </summary>
        TimeSpan TimeToLive { get; }

        /// <summary>
        /// Remove all expired items from the cache.
        /// </summary>
        void TrimExpired();
    }

    public class NoneTimePolicy : ITimePolicy
    {
        public static readonly TimeSpan Infinite = new TimeSpan(0, 0, 0, 0, -1);

        public static NoneTimePolicy Instance = new NoneTimePolicy();

        public bool CanExpire => false;

        public TimeSpan TimeToLive => Infinite;

        public void TrimExpired()
        {
        }
    }
}
