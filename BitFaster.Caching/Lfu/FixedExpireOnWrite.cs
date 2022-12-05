using System;

namespace BitFaster.Caching.Lfu
{
    ////////////////////////////////////////////////////////// Plan
    //
    // LFU maintenance steps
    // 0. At initializion, set the eviction callback
    // 1. At the start of maintanence, SetTime
    // 2. When processing add/update buffers, call UpdateTimeToLive
    // 3. Before Eviction, call ExpireEntries
    //
    // TODO:
    // - LfuNodeList is generic in terms of item type
    // - Verify base scenario perf is not regressed due to array writes etc.
    // - Check item is expired on read
    // - LfuCore has a generic type for time expiry policy
    // - TrimExpired: update the time, then call ExpireEntries
    // - Update builder

    internal interface ILfuTimePolicy<K, V>
    {
        void SetEvictionCallback(Action<LfuNode<K, V>> evict);

        void SetTime();

        void UpdateTimeToLive(LfuNode<K, V> item);

        void ExpireEntries(LfuNodeList<K, V> windowLru, LfuNodeList<K, V> probationLru, LfuNodeList<K, V> protectedLru);
    }

    internal struct NoTimePolicy<K, V> : ILfuTimePolicy<K, V>
    {
        public void SetEvictionCallback(Action<LfuNode<K, V>> evict)
        {
        }

        public void SetTime()
        {
        }

        public void UpdateTimeToLive(LfuNode<K, V> item)
        {
        }

        public void ExpireEntries(LfuNodeList<K, V> windowLru, LfuNodeList<K, V> probationLru, LfuNodeList<K, V> protectedLru)
        {
        }
    }

    internal struct FixedExpireOnWrite<K, V> : ILfuTimePolicy<K, V>
    {
        private readonly long timeToLive;
        private Action<LfuNode<K, V>> evict;

        public FixedExpireOnWrite(TimeSpan timeToLive)
        {
            this.timeToLive = 0;// ToTicks(timeToLive);
            this.Now = 0;
            evict = null;
        }

        public long Now { get; private set; }

        public void SetEvictionCallback(Action<LfuNode<K, V>> evict)
        {
            this.evict = evict;
        }

        public void SetTime()
        {
            // get the current time once
            // set the ttl to use for SetExpiry
        }

        public void UpdateTimeToLive(LfuNode<K, V> item)
        { 
            // set the item time == now
        }

        public void ExpireEntries(LfuNodeList<K, V> windowLru, LfuNodeList<K, V> probationLru, LfuNodeList<K, V> protectedLru)
        {
            Expire(windowLru);
            Expire(probationLru);
            Expire(protectedLru);
        }

        // for each list, start at tail and remove items with itemTime > Now 
        private void Expire(LfuNodeList<K, V> list)
        {
            // TODO: how to actually delete the items?
            // * Callback function
            // - reference to LFU, call internal/public methods
            // - write items to a buffer, then loop and remove
            // - internal method
            // - put node lists + dictionary into a class, with remove methods etc. then pass that
            // - make an evictor class, put eviction logic there

            var end = list.Last;

            while (end != null)
            {
                var prev = end.prev;

                if (true) // end expired
                {
                    evict(end);
                }

                end = prev;
            }
        }
    }
}
