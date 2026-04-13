using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching
{
    internal static class CollectionExtensions
    {
#if NET9_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCompatibleKey<TAlternateKey, TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary)
            where TAlternateKey : notnull, allows ref struct
            where TKey : notnull
        {
            return dictionary.Comparer is IAlternateEqualityComparer<TAlternateKey, TKey>;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IAlternateEqualityComparer<TAlternateKey, TKey> GetAlternateComparer<TAlternateKey, TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary)
            where TAlternateKey : notnull, allows ref struct
            where TKey : notnull
        {
            Debug.Assert(dictionary.IsCompatibleKey<TAlternateKey, TKey, TValue>());
            return Unsafe.As<IAlternateEqualityComparer<TAlternateKey, TKey>>(dictionary.Comparer!);
        }
#endif
    }
}
