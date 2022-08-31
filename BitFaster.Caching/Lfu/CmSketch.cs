using System;
using System.Collections.Generic;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// A probabilistic data structure used to estimate the frequency of a given value. Periodic aging reduces the
    /// accumulated count across all values over time, such that a historic popular value will decay to zero frequency
    /// over time if it is not accessed.
    /// </summary>
    /// <remarks>
    /// This is a direct C# translation of FrequencySketch in the Caffeine library by ben.manes@gmail.com (Ben Manes).
    /// http://www.apache.org/licenses/LICENSE-2.0
    /// </remarks>
    public sealed class CmSketch<T>
    {
        // A mixture of seeds from FNV-1a, CityHash, and Murmur3
        private static ulong[] Seed = { 0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L};
        private static long ResetMask = 0x7777777777777777L;
        private static long OneMask = 0x1111111111111111L;

        private int sampleSize;
        private int tableMask;
        private long[] table;
        private int size;

        private readonly IEqualityComparer<T> comparer;

        public CmSketch(long maximumSize, IEqualityComparer<T> comparer)
        {
            EnsureCapacity(maximumSize);
            this.comparer = comparer;
        }

        public int ResetSampleSize => this.sampleSize;

        public int Size => this.size;

        public void EnsureCapacity(long maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, int.MaxValue >> 1);

            table = new long[(maximum == 0) ? 1 : BitOps.CeilingPowerOfTwo(maximum)];
            tableMask = Math.Max(0, table.Length - 1);
            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

            size = 0;
        }

        public int EstimateFrequency(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));

            int start = (hash & 3) << 2;
            int frequency = int.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                int index = IndexOf(hash, i);
                int count = (int)(((ulong)table[index] >> ((start + i) << 2)) & 0xfL);
                frequency = Math.Min(frequency, count);
            }
            return frequency;
        }

        public void Increment(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));
            int start = (hash & 3) << 2;

            // Loop unrolling improves throughput by 5m ops/s
            int index0 = IndexOf(hash, 0);
            int index1 = IndexOf(hash, 1);
            int index2 = IndexOf(hash, 2);
            int index3 = IndexOf(hash, 3);

            bool added = IncrementAt(index0, start);
            added |= IncrementAt(index1, start + 1);
            added |= IncrementAt(index2, start + 2);
            added |= IncrementAt(index3, start + 3);

            if (added && (++size == sampleSize))
            {
                Reset();
            }
        }

        private bool IncrementAt(int i, int j)
        {
            int offset = j << 2;
            long mask = (0xfL << offset);
            if ((table[i] & mask) != mask)
            {
                table[i] += (1L << offset);
                return true;
            }
            return false;
        }

        private void Reset()
        {
            int count = 0;
            for (int i = 0; i < table.Length; i++)
            {
                count += BitOps.BitCount(table[i] & OneMask);
                table[i] = (long)((ulong)table[i] >> 1) & ResetMask;
            }
            size = (size - (count >> 2)) >> 1;
        }

        public void Clear()
        {
            table = new long[table.Length];
            size = 0;
        }

        private int IndexOf(int item, int i)
        {
            ulong hash = ((ulong)item + Seed[i]) * Seed[i];
            hash += (hash >> 32);
            return ((int)hash) & tableMask;
        }

        private int Spread(int x)
        {
            uint y = (uint)x;
            y = ((y >> 16) ^ y) * 0x45d9f3b;
            y = ((y >> 16) ^ y) * 0x45d9f3b;
            return (int)((y >> 16) ^ y);
        }
    }
}
