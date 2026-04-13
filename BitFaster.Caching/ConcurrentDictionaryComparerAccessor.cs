using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace BitFaster.Caching
{
    internal static class ConcurrentDictionaryComparerAccessor<TKey, TValue>
        where TKey : notnull
    {
        internal static IEqualityComparer<TKey> Get(ConcurrentDictionary<TKey, TValue> dictionary)
        {
#if NET6_0_OR_GREATER
            return dictionary.Comparer;
#else
            object? tables = tablesField.GetValue(dictionary);
            return (IEqualityComparer<TKey>)comparerField.GetValue(tables)!;
#endif
        }

#if !NET6_0_OR_GREATER
        private static readonly FieldInfo tablesField = typeof(ConcurrentDictionary<TKey, TValue>).GetField("_tables", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ConcurrentDictionary<TKey, TValue>).FullName, "_tables");

        private static readonly FieldInfo comparerField = tablesField.FieldType.GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? tablesField.FieldType.GetField("m_comparer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(tablesField.FieldType.FullName, "_comparer");
#endif
    }
}
