using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedAsyncCacheTests : ScopedAsyncCacheTestBase
    {
        public ScopedAsyncCacheTests() 
            : base(new ScopedAsyncCache<int, Disposable>(new ConcurrentLru<int, Scoped<Disposable>>(capacity)))
        {
        }
    }
}
