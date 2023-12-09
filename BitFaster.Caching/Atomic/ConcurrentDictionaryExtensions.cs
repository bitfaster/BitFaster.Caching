using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// Convenience methods for using AtomicFactory with ConcurrentDictionary. 
    /// </summary>
    public static class ConcurrentDictionaryExtensions
    {
        /// <summary>
        /// Adds a key/value pair to the ConcurrentDictionary if the key does not already exist. Returns the new value, or the existing value if the key already exists.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
        public static V GetOrAdd<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> dictionary, K key, Func<K, V> valueFactory)
             where K : notnull
        {
            var atomicFactory = dictionary.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory);
        }

        /// <summary>
        /// Adds a key/value pair to the ConcurrentDictionary by using the specified function and an argument if the key does not already exist, or returns the existing value if the key exists.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
        public static V GetOrAdd<K, V, TArg>(this ConcurrentDictionary<K, AtomicFactory<K, V>> dictionary, K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
             where K : notnull
        {
            var atomicFactory = dictionary.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory, factoryArgument);
        }

        /// <summary>
        /// Adds a key/value pair to the ConcurrentDictionary if the key does not already exist. Returns the new value, or the existing value if the key already exists.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
        public static ValueTask<V> GetOrAddAsync<K, V>(this ConcurrentDictionary<K, AsyncAtomicFactory<K, V>> dictionary, K key, Func<K, Task<V>> valueFactory)
             where K : notnull
        {
            var asyncAtomicFactory = dictionary.GetOrAdd(key, _ => new AsyncAtomicFactory<K, V>());
            return asyncAtomicFactory.GetValueAsync(key, valueFactory);
        }

        /// <summary>
        /// Adds a key/value pair to the ConcurrentDictionary by using the specified function and an argument if the key does not already exist, or returns the existing value if the key exists.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
        public static ValueTask<V> GetOrAddAsync<K, V, TArg>(this ConcurrentDictionary<K, AsyncAtomicFactory<K, V>> dictionary, K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
             where K : notnull
        {
            var asyncAtomicFactory = dictionary.GetOrAdd(key, _ => new AsyncAtomicFactory<K, V>());
            return asyncAtomicFactory.GetValueAsync(key, valueFactory, factoryArgument);
        }

        /// <summary>
        /// Attempts to get the value associated with the specified key from the ConcurrentDictionary.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the object from the ConcurrentDictionary that has the specified key, or the default value of the type if the operation failed.</param>
        /// <returns>true if the key was found in the ConcurrentDictionary; otherwise, false.</returns>
        public static bool TryGetValue<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> dictionary, K key, [MaybeNullWhen(false)] out V value)
             where K : notnull
        {
            AtomicFactory<K, V>? output;
            var ret = dictionary.TryGetValue(key, out output);

            if (ret && output!.IsValueCreated)
            {
                value = output.ValueIfCreated!;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to get the value associated with the specified key from the ConcurrentDictionary.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the object from the ConcurrentDictionary that has the specified key, or the default value of the type if the operation failed.</param>
        public static bool TryGetValue<K, V>(this ConcurrentDictionary<K, AsyncAtomicFactory<K, V>> dictionary, K key, [MaybeNullWhen(false)] out V value)
             where K : notnull
        {
            AsyncAtomicFactory<K, V>? output;
            var ret = dictionary.TryGetValue(key, out output);

            if (ret && output!.IsValueCreated)
            {
                value = output.ValueIfCreated!;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Removes a key and value from the dictionary.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="item">The KeyValuePair representing the key and value to remove.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public static bool TryRemove<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> dictionary, KeyValuePair<K, V> item)
             where K : notnull
        {
            var kvp = new KeyValuePair<K, AtomicFactory<K, V>>(item.Key, new AtomicFactory<K, V>(item.Value));
#if NET6_0_OR_GREATER
            return dictionary.TryRemove(kvp);
#else
            // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
            return ((ICollection<KeyValuePair<K, AtomicFactory<K, V>>>)dictionary).Remove(kvp);
#endif
        }

        /// <summary>
        /// Removes a key and value from the dictionary.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="item">The KeyValuePair representing the key and value to remove.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public static bool TryRemove<K, V>(this ConcurrentDictionary<K, AsyncAtomicFactory<K, V>> dictionary, KeyValuePair<K, V> item)
             where K : notnull
        {
            var kvp = new KeyValuePair<K, AsyncAtomicFactory<K, V>>(item.Key, new AsyncAtomicFactory<K, V>(item.Value));
#if NET6_0_OR_GREATER
            return dictionary.TryRemove(kvp);
#else
            // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
            return ((ICollection<KeyValuePair<K, AsyncAtomicFactory<K, V>>>)dictionary).Remove(kvp);
#endif
        }

        /// <summary>
        /// Attempts to remove and return the value that has the specified key from the ConcurrentDictionary.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the element to remove and return.</param>
        /// <param name="value">When this method returns, contains the object removed from the ConcurrentDictionary, or the default value of the TValue type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public static bool TryRemove<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> dictionary, K key, [MaybeNullWhen(false)] out V value)
             where K : notnull
        {
            if (dictionary.TryRemove(key, out var atomic))
            {
                value = atomic.ValueIfCreated!;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to remove and return the value that has the specified key from the ConcurrentDictionary.
        /// </summary>
        /// <param name="dictionary">The ConcurrentDictionary to use.</param>
        /// <param name="key">The key of the element to remove and return.</param>
        /// <param name="value">When this method returns, contains the object removed from the ConcurrentDictionary, or the default value of the TValue type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public static bool TryRemove<K, V>(this ConcurrentDictionary<K, AsyncAtomicFactory<K, V>> dictionary, K key, [MaybeNullWhen(false)] out V value)
             where K : notnull
        {
            if (dictionary.TryRemove(key, out var atomic))
            {
                value = atomic.ValueIfCreated!;
                return true;
            }

            value = default;
            return false;
        }
    }
}
