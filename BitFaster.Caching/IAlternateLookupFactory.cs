#if NET9_0_OR_GREATER
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BitFaster.Caching
{
    /// <summary>
    /// Internal interface that enables cache decorators to obtain an alternate lookup from the inner cache.
    /// </summary>
    internal interface IAlternateLookupFactory<K, V> where K : notnull
    {
        IAlternateLookup<TAlternateKey, K, V> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct;

        bool TryGetAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct;

        IAlternateEqualityComparer<TAlternateKey, K> GetAlternateComparer<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct;
    }
}
#endif
