using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public interface ITimestampedPolicy<K, V> : IPolicy<K, V, TimeStampedLruItem<K, V>> 
    {
    }
}
