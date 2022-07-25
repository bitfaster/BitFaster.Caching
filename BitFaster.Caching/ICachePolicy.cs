using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public interface ICachePolicy<K>
    {
        BoundedPolicy Eviction { get; }

        TimePolicy<K> ExpireAfterWrite { get; }

        bool IsWriteAtomic { get; }
    }

    public class BoundedPolicy
    {
        public static readonly BoundedPolicy None = new BoundedPolicy(i => { });

        private readonly Action<int> trimCallback;

        public BoundedPolicy(Action<int> trimCallback)
        { 
            this.trimCallback = trimCallback;
        }

        public void Trim(int itemCount)
        {
            trimCallback(itemCount);
        }
    }

    public class TimePolicy<K>
    {
        public static readonly TimePolicy<K> None = new TimePolicy<K>();

        public TimeSpan TimeToLive { get; }

        public TimeSpan? AgeOf(K key)
        {
            return null;
        }

        public void TrimExpired()
        { 
        }
    }
}
