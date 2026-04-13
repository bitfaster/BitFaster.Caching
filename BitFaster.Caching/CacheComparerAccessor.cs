using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

namespace BitFaster.Caching
{
    internal static class CacheComparerAccessor<TKey>
        where TKey : notnull
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IEqualityComparer<TKey>>> accessors = new();

        internal static IEqualityComparer<TKey> Get(object cache)
        {
            return accessors.GetOrAdd(cache.GetType(), CreateAccessor)(cache);
        }

#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Comparer is read from public cache types that expose a public Comparer property.")]
#endif
        private static Func<object, IEqualityComparer<TKey>> CreateAccessor(Type type)
        {
            var property = type.GetProperty("Comparer", BindingFlags.Instance | BindingFlags.Public);

            if (property?.GetMethod == null || !typeof(IEqualityComparer<TKey>).IsAssignableFrom(property.PropertyType))
            {
                return static _ => throw new NotSupportedException("Comparer is not available because the underlying cache does not expose a compatible Comparer property.");
            }

            return cache => (IEqualityComparer<TKey>)property.GetValue(cache)!;
        }
    }
}
