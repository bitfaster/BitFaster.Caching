using System.Collections.Generic;

namespace BitFaster.Caching
{
    internal interface ICacheComparer<K>
        where K : notnull
    {
        IEqualityComparer<K> Comparer { get; }
    }
}
