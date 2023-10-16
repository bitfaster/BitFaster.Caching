using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HashTableSize
{
    public class HashTools
    {
        // skip prime number 2, which is first element
        internal static ReadOnlySpan<int> Primes => GetPrimesUpTo(1000).Skip(1).ToArray();

        //    new int[]
        //{
        //    3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
        //    1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
        //    17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
        //    187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
        //    1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
        //};
  
        internal static int NextPrimeGreaterThan(int min)
        {
            foreach (int prime in Primes)
            {
                if (prime >= min)
                {
                    return prime;
                }
            }

            return 7199369;
        }

        // Replicates .NET framework ConcurrentDictionary resize logic:
        // https://github.com/microsoft/referencesource/blob/51cf7850defa8a17d815b4700b67116e3fa283c2/mscorlib/system/collections/Concurrent/ConcurrentDictionary.cs#L1828C29-L1828C29
        internal static bool TryNextTableSize(int initial, out int next)
        {
            try
            {
                checked
                {
                    // Double the size of the buckets table and add one, so that we have an odd integer.
                    int newLength = initial * 2 + 1;

                    // Now, we only need to check odd integers, and find the first that is not divisible
                    // by 3, 5 or 7.
                    while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
                    {
                        newLength += 2;
                    }

                    next = newLength;
                    return true;
                }
            }
            catch (OverflowException) 
            {
                next = 0;
                return false;
            }
        }

        // https://stackoverflow.com/questions/239865/best-way-to-find-all-factors-of-a-given-number
        internal static List<int> Factor(int number)
        {
            var factors = new List<int>();
            int max = (int)Math.Sqrt(number);  // Round down

            for (int factor = 1; factor <= max; ++factor) // Test from 1 to the square root, or the int below it, inclusive.
            {
                if (number % factor == 0)
                {
                    factors.Add(factor);
                    if (factor != number / factor) // Don't add the square root twice!  Thanks Jon
                        factors.Add(number / factor);
                }
            }

            factors.Remove(1);
            factors.Remove(number);
            factors.Sort();

            return factors;
        }

        // https://stackoverflow.com/questions/1510124/program-to-find-prime-numbers
        public static IEnumerable<int> GetPrimesUpTo(int num)
        {
            return Enumerable.Range(0, (int)Math.Floor(2.52 * Math.Sqrt(num) / Math.Log(num))).Aggregate(
                Enumerable.Range(2, num - 1).ToList(),
                (result, index) =>
                {
                    var bp = result[index]; var sqr = bp * bp;
                    result.RemoveAll(i => i >= sqr && i % bp == 0);
                    return result;
                }
            );
        }

        public static int ChooseInitialSize(int targetSize, int initialSize)
        {
            int tenPercent = (int)(targetSize * 0.1);

            while (initialSize < tenPercent || initialSize < 131)
            {
                if (!TryNextTableSize(initialSize, out int newInitial))
                {
                    break;
                }
                initialSize = newInitial;
            }

            return initialSize;
        }
    }
}
