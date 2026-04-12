using System.Collections.Generic;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides access to the equality comparer used by a cache.
    /// </summary>
    internal interface ICacheComparer<K>
        where K : notnull
    {
        IEqualityComparer<K> Comparer { get; }
    }
}
