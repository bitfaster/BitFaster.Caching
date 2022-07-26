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
}
