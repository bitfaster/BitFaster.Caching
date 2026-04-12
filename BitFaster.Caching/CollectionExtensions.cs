using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if !NET9_0_OR_GREATER
using System.Reflection;
#endif

namespace BitFaster.Caching
{
    internal static class CollectionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IEqualityComparer<TKey> GetComparer<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary)
            where TKey : notnull
        {
#if NET9_0_OR_GREATER
            return dictionary.Comparer;
#else
            object? tables = ConcurrentDictionaryFields<TKey, TValue>.tables.GetValue(dictionary);
            return (IEqualityComparer<TKey>)ConcurrentDictionaryFields<TKey, TValue>.comparer.GetValue(tables)!;
#endif
        }

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

#if !NET9_0_OR_GREATER
        private static class ConcurrentDictionaryFields<TKey, TValue>
            where TKey : notnull
        {
            internal static readonly FieldInfo tables = typeof(ConcurrentDictionary<TKey, TValue>).GetField("_tables", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(ConcurrentDictionary<TKey, TValue>).FullName, "_tables");

            internal static readonly FieldInfo comparer = tables.FieldType.GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? tables.FieldType.GetField("m_comparer", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(tables.FieldType.FullName, "_comparer");
        }
#endif
    }
}
