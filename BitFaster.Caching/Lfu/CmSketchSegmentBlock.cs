using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    public class CmSketchSegmentBlock<T, I> where I : struct, IsaProbe
    {
        private static readonly long ResetMask = 0x7777777777777777L;
        private static readonly long OneMask = 0x1111111111111111L;

        private int sampleSize;
        private int blockMask;
        private long[] table;
        private int size;

        private readonly IEqualityComparer<T> comparer;

        public CmSketchSegmentBlock(long maximumSize, IEqualityComparer<T> comparer)
        {
            EnsureCapacity(maximumSize);
            this.comparer = comparer;
        }

        public int ResetSampleSize => this.sampleSize;

        public int Size => this.size;

        public int EstimateFrequency(T value)
        {
            int[] count = new int[4];
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            for (int i = 0; i < 4; i++)
            {
                int h = (int)((uint)counterHash >> (i << 3));
                int index = (h >> 3) & 15;
                int offset = h & 7;
                count[i] = (int)(((ulong)table[block + offset] >> (index << 2)) & 0xfL);
            }
            return Math.Min(Math.Min(count[0], count[1]), Math.Min(count[2], count[3]));
        }

        public void Increment(T value)
        {
            int[] index = new int[8];
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            for (int i = 0; i < 4; i++)
            {
                int h = (int)((uint)counterHash >> (i << 3));
                index[i] = (h >> 3) & 15;
                int offset = h & 7;
                index[i + 4] = block + offset;
            }

            bool added =
                  IncrementAt(index[4], index[0])
                | IncrementAt(index[5], index[1])
                | IncrementAt(index[6], index[2])
                | IncrementAt(index[7], index[3]);

            if (added && (++size == sampleSize))
            {
                Reset();
            }
        }

        public void Clear()
        {
            table = new long[table.Length];
            size = 0;
        }

        private void EnsureCapacity(long maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, int.MaxValue >> 1);

            table = new long[Math.Max(BitOps.CeilingPowerOfTwo(maximum), 8)];
            blockMask = (int)((uint)table.Length >> 3) - 1;
            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

            size = 0;
        }

        // Applies another round of hashing for additional randomization
        int Rehash(int x)
        {
            x *= (x + 0x31848bab) * 0x31848bab;
            x ^= (int)((uint)x >> 14);
            return x;
        }

        // Applies a supplemental hash functions to defends against poor quality hash.
        private int Spread(int x)
        {
            x ^= (int)((uint)x >> 17);
            x = (int)((x + 0xed5ad4bb) * 0xed5ad4bb);
            x ^= (int)((uint)x >> 11);
            x = (int)((x + 0xac4c1b51) * 0xac4c1b51);
            x ^= (int)((uint)x >> 15);
            return x;
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
            // unroll, almost 2x faster
            int count0 = 0;
            int count1 = 0;
            int count2 = 0;
            int count3 = 0;

            for (int i = 0; i < table.Length; i += 4)
            {
                count0 += BitOps.BitCount(table[i] & OneMask);
                count1 += BitOps.BitCount(table[i + 1] & OneMask);
                count2 += BitOps.BitCount(table[i + 2] & OneMask);
                count3 += BitOps.BitCount(table[i + 3] & OneMask);

                table[i] = (long)((ulong)table[i] >> 1) & ResetMask;
                table[i + 1] = (long)((ulong)table[i + 1] >> 1) & ResetMask;
                table[i + 2] = (long)((ulong)table[i + 2] >> 1) & ResetMask;
                table[i + 3] = (long)((ulong)table[i + 3] >> 1) & ResetMask;
            }

            count0 = (count0 + count1) + (count2 + count3);

            size = (size - (count0 >> 2)) >> 1;
        }
    }
}
