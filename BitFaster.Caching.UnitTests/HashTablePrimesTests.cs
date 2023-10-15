using System.Collections.Generic;
using System;
using BitFaster.Caching;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests
{
    public class HashTablePrimesTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public HashTablePrimesTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(3, 7)]
        [InlineData(8, 11)]
        [InlineData(12, 17)]
        [InlineData(132, 137)]
        [InlineData(500, 137)]
        public void NextPrimeGreaterThan(int input, int nextPrime)
        {
            HashTablePrimes.NextPrimeGreaterThan(input).Should().Be(nextPrime);
        }

        // This test method replicates the hash table sizes that will be computed by ConcurrentDictionary
        // on earlier versions of .NET before prime numbers are used.
        // 277 prime
        // 557 prime
        // 1117 prime
        // 2237 prime
        // 4477 has factors 11, 37, 121, 407
        // 8957 has factors 13, 53, 169, 689
        // 17917 has factors 19, 23, 41, 437, 779, 943
        // 35837 prime
        // 71677 has factors 229, 313
        // 143357 prime
        // 286717 has factors 163, 1759
        // 573437 prime
        // 1146877 prime
        // 2293757 prime
        // 4587517 has factors 11, 103, 1133, 4049, 44539, 417047
        // 9175037 prime
        // 18350077 has factors 701, 26177
        // 36700157 has factors 13, 23, 299, 122743, 1595659, 2823089
        // 73400317 has factors 4999, 14683
        // 146800637 prime
        // 293601277 has factors 6113, 48029
        // 587202557 has factors 1877, 312841
        // 1174405117 has factors 10687, 109891
        [Fact(Skip="Not a functional test")]
        public void ComputeHashTableSizes()
        {
            // candidates: 137, 151, 163, 211
            int size = 137;
            for (int i = 0; i < 23; i++)
            {
                int nextSize = NextTableSize(size);
                this.testOutputHelper.WriteLine($"{nextSize} {GetFactorsString(nextSize)}");
                size = nextSize;
            }
        }

        // Replicates .NET framework ConcurrentDictionary resize logic:
        // https://github.com/microsoft/referencesource/blob/51cf7850defa8a17d815b4700b67116e3fa283c2/mscorlib/system/collections/Concurrent/ConcurrentDictionary.cs#L1828C29-L1828C29
        private static int NextTableSize(int initial)
        {
            // Double the size of the buckets table and add one, so that we have an odd integer.
            int newLength = initial * 2 + 1;

            // Now, we only need to check odd integers, and find the first that is not divisible
            // by 3, 5 or 7.
            while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
            {
                newLength += 2;
            }

            return newLength;
        }

        private static string GetFactorsString(int nextSize)
        {
            var factors = Factor(nextSize);

            factors.Remove(1);
            factors.Remove(nextSize);
            factors.Sort();

            if (factors.Count == 0)
            {
                return "prime";
            }

            return $"has factors {string.Join(", ", factors)}";
        }

        // https://stackoverflow.com/questions/239865/best-way-to-find-all-factors-of-a-given-number
        private static List<int> Factor(int number)
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

            return factors;
        }
    }
}
