using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching
{
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
