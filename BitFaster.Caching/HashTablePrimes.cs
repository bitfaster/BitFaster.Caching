using System;

namespace BitFaster.Caching
{
    // Using the capacity passed into the cache ctor to initialize the ConcurrentDictionary has 2 problems:
    //
    // 1. By allocating up front, we eliminate resizing. However, if the capacity is very large and the cache is not used,
    // we will waste a lot of memory.
    // 2. On earlier versions of .NET, ConcurrentDictionary uses the capacity arg to directly initialize the hash table
    // size. On resize, the hashtable is grown to 2x + 1 while avoiding factors of 3, 5, or 7 (but not larger). On
    // newer versions of.NET, both initial size and resize is based the next prime number larger than capacity. Collisions
    // are reduced when hash table size is prime. Hence the change to use primes in all cases in newer versions of the
    // framework.
    //
    // To mitigate this, we adopt a simple scheme: find the next prime larger than the capacity arg, up to 137. If the
    // capacity is greater than 137, just set the initial size to 137, thereby bounding initial memory consumption for
    // large caches.
    // 
    // - Older.NET implementations: For smaller caches, we fix size at the next largest prime. For larger tables, we now
    // start out with a larger prime (avoiding all factors up to 137, not just 3, 5 and 7). Above 137, some sizes will be
    // prime and others have relatively few factors.The complete list is given as a comment in the unit test code.
    // - Newer.NET implementations: as above for smaller caches. For larger caches, the resize will use successively larger
    // primes.The duplicate prime computation added is only during construction and is effectively a no-op.
    internal class HashTablePrimes
    {
#if NETSTANDARD2_0
        internal static int[] Primes = new int[] {
#else
        internal static ReadOnlySpan<int> Primes => new int[] {
#endif
            7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131
        };
        internal static int NextPrimeGreaterThan(int min)
        {
            foreach (int prime in Primes)
            {
                if (prime > min)
                {
                    return prime;
                }
            }

            return 137;
        }
    }
}
