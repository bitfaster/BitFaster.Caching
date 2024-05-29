using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;




#if !NETSTANDARD2_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// A probabilistic data structure used to estimate the frequency of a given value. Periodic aging reduces the
    /// accumulated count across all values over time, such that a historic popular value will decay to zero frequency
    /// over time if it is not accessed.
    /// </summary>
    /// <remarks>
    /// The maximum frequency of an element is limited to 15 (4-bits). Each element is hashed to a 64 byte 'block'
    /// consisting of 4 segments of 32 4-bit counters. The 64 byte blocks are the same size as x64 L1 cache lines.
    /// While the blocks are not guaranteed to be aligned, this scheme minimizes L1 cache misses resulting in a
    /// significant speedup. When supported, a vectorized AVX2 code path provides a further speedup. Together, block 
    /// and AVX2 are approximately 2x faster than the original implementation.
    /// </remarks>
    /// This is a direct C# translation of FrequencySketch in the Caffeine library by ben.manes@gmail.com (Ben Manes).
    /// https://github.com/ben-manes/caffeine
    public unsafe class CmSketchCore<T, I>
        where T : notnull
        where I : struct, IsaProbe
    {
        private const long ResetMask = 0x7777777777777777L;
        private const long OneMask = 0x1111111111111111L;

        private long[] table;
        private long* tableAddr;
        private int sampleSize;
        private int blockMask;
        private int size;

        private readonly IEqualityComparer<T> comparer;

        /// <summary>
        /// Initializes a new instance of the CmSketch class with the specified maximum size and equality comparer.
        /// </summary>
        /// <param name="maximumSize">The maximum size.</param>
        /// <param name="comparer">The equality comparer.</param>
        public CmSketchCore(long maximumSize, IEqualityComparer<T> comparer)
        {
            EnsureCapacity(maximumSize);
            this.comparer = comparer;
        }

        /// <summary>
        /// Gets the reset sample size.
        /// </summary>
        public int ResetSampleSize => this.sampleSize;

        /// <summary>
        /// Gets the size.
        /// </summary>
        public int Size => this.size;

        /// <summary>
        /// Estimate the frequency of the specified value, up to the maximum of 15.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The estimated frequency of the value.</returns>
        public int EstimateFrequency(T value)
        {
#if NETSTANDARD2_0
            return EstimateFrequencyStd(value);
#else

            I isa = default;

            if (isa.IsAvx2Supported)
            {
                return EstimateFrequencyAvx(value);
            }
            else
            {
                return EstimateFrequencyStd(value);
            }
#endif
        }

        /// <summary>
        /// Increment the count of the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Increment(T value)
        {
#if NETSTANDARD2_0
            IncrementStd(value);
#else

            I isa = default;

            if (isa.IsAvx2Supported)
            {
                IncrementAvx(value);
            }
            else
            {
                IncrementStd(value);
            }
#endif
        }

        /// <summary>
        /// Clears the count for all items.
        /// </summary>
        public void Clear()
        {
            #if NET6_0_OR_GREATER
            table = GC.AllocateArray<long>(table.Length, true);
                        GCHandle handle = GCHandle.Alloc(table, GCHandleType.Pinned);
            IntPtr pointer = handle.AddrOfPinnedObject();

            tableAddr = (long*)pointer + pointer.ToInt64() % 32;
            #else
            table = new long[table.Length];
            #endif
            size = 0;
        }

        [MemberNotNull(nameof(table))]
        private void EnsureCapacity(long maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, int.MaxValue >> 1);

            #if NET6_0_OR_GREATER
            // over alloc by 4 to give 32 byte buffer
            table = GC.AllocateArray<long>(Math.Max(BitOps.CeilingPowerOfTwo(maximum), 8) + 4, true);

            GCHandle handle = GCHandle.Alloc(table, GCHandleType.Pinned);
            IntPtr pointer = handle.AddrOfPinnedObject();

            tableAddr = (long*)pointer + pointer.ToInt64() % 32;

            #else
            table = new long[Math.Max(BitOps.CeilingPowerOfTwo(maximum), 8)];
            #endif
            blockMask = (int)((uint)(table.Length-4) >> 3) - 1;
            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

            size = 0;
        }

        private unsafe int EstimateFrequencyStd(T value)
        {
            var count = stackalloc int[4];
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            for (int i = 0; i < 4; i++)
            {
                int h = (int)((uint)counterHash >> (i << 3));
                int index = (h >> 1) & 15;
                int offset = h & 1;
                count[i] = (int)(((ulong)table[block + offset + (i << 1)] >> (index << 2)) & 0xfL);
            }
            return Math.Min(Math.Min(count[0], count[1]), Math.Min(count[2], count[3]));
        }

        private unsafe void IncrementStd(T value)
        {
            var index = stackalloc int[8];
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            for (int i = 0; i < 4; i++)
            {
                int h = (int)((uint)counterHash >> (i << 3));
                index[i] = (h >> 1) & 15;
                int offset = h & 1;
                index[i + 4] = block + offset + (i << 1);
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

        // Applies another round of hashing for additional randomization
        private static int Rehash(int x)
        {
            x = (int)(x * 0x31848bab);
            x ^= (int)((uint)x >> 14);
            return x;
        }

        // Applies a supplemental hash functions to defends against poor quality hash.
        private static int Spread(int x)
        {
            x ^= (int)((uint)x >> 17);
            x = (int)(x * 0xed5ad4bb);
            x ^= (int)((uint)x >> 11);
            x = (int)(x * 0xac4c1b51);
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

#if !NETSTANDARD2_0
        private unsafe int EstimateFrequencyAvx(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = Vector128.Create(counterHash);
            h = Avx2.ShiftRightLogicalVariable(h.AsUInt32(), Vector128.Create(0U, 8U, 16U, 24U)).AsInt32();

            var index = Avx2.ShiftRightLogical(h, 1);
            index = Avx2.And(index, Vector128.Create(15)); // j - counter index
            Vector128<int> offset = Avx2.And(h, Vector128.Create(1));
            Vector128<int> blockOffset = Avx2.Add(Vector128.Create(block), offset); // i - table index
            blockOffset = Avx2.Add(blockOffset, Vector128.Create(0, 2, 4, 6)); // + (i << 1)

            long* tablePtr = tableAddr;
            {
                Vector256<long> tableVector = Avx2.GatherVector256(tablePtr, blockOffset, 8);
                index = Avx2.ShiftLeftLogical(index, 2);

                // convert index from int to long via permute
                Vector256<long> indexLong = Vector256.Create(index, Vector128<int>.Zero).AsInt64();
                Vector256<int> permuteMask2 = Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7);
                indexLong = Avx2.PermuteVar8x32(indexLong.AsInt32(), permuteMask2).AsInt64();
                tableVector = Avx2.ShiftRightLogicalVariable(tableVector, indexLong.AsUInt64());
                tableVector = Avx2.And(tableVector, Vector256.Create(0xfL));

                Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
                Vector128<ushort> count = Avx2.PermuteVar8x32(tableVector.AsInt32(), permuteMask)
                    .GetLower()
                    .AsUInt16();

                // set the zeroed high parts of the long value to ushort.Max
#if NET6_0
                count = Avx2.Blend(count, Vector128<ushort>.AllBitsSet, 0b10101010);
#else
                count = Avx2.Blend(count, Vector128.Create(ushort.MaxValue), 0b10101010);
#endif

                return Avx2.MinHorizontal(count).GetElement(0);
            }
        }

        private unsafe void IncrementAvx(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = Vector128.Create(counterHash);
            h = Avx2.ShiftRightLogicalVariable(h.AsUInt32(), Vector128.Create(0U, 8U, 16U, 24U)).AsInt32();

            Vector128<int> index = Avx2.ShiftRightLogical(h, 1);
            index = Avx2.And(index, Vector128.Create(15)); // j - counter index
            Vector128<int> offset = Avx2.And(h, Vector128.Create(1));
            Vector128<int> blockOffset = Avx2.Add(Vector128.Create(block), offset); // i - table index
            blockOffset = Avx2.Add(blockOffset, Vector128.Create(0, 2, 4, 6)); // + (i << 1)

            //fixed (long* tablePtr = table)
                long* tablePtr = tableAddr;
            {
                Vector256<long> tableVector = Avx2.GatherVector256(tablePtr, blockOffset, 8);

                // j == index
                index = Avx2.ShiftLeftLogical(index, 2);
                Vector256<long> offsetLong = Vector256.Create(index, Vector128<int>.Zero).AsInt64();

                Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7);
                offsetLong = Avx2.PermuteVar8x32(offsetLong.AsInt32(), permuteMask).AsInt64();

                // mask = (0xfL << offset)
                Vector256<long> fifteen = Vector256.Create(0xfL);
                Vector256<long> mask = Avx2.ShiftLeftLogicalVariable(fifteen, offsetLong.AsUInt64());

                // (table[i] & mask) != mask)
                // Note masked is 'equal' - therefore use AndNot below
                Vector256<long> masked = Avx2.CompareEqual(Avx2.And(tableVector, mask), mask);

                // 1L << offset
                Vector256<long> inc = Avx2.ShiftLeftLogicalVariable(Vector256.Create(1L), offsetLong.AsUInt64());

                // Mask to zero out non matches (add zero below) - first operand is NOT then AND result (order matters)
                inc = Avx2.AndNot(masked, inc);

                Vector256<byte> result = Avx2.CompareEqual(masked.AsByte(), Vector256<byte>.Zero);
                bool wasInc = Avx2.MoveMask(result.AsByte()) == unchecked((int)(0b1111_1111_1111_1111_1111_1111_1111_1111));

                tablePtr[blockOffset.GetElement(0)] += inc.GetElement(0);
                tablePtr[blockOffset.GetElement(1)] += inc.GetElement(1);
                tablePtr[blockOffset.GetElement(2)] += inc.GetElement(2);
                tablePtr[blockOffset.GetElement(3)] += inc.GetElement(3);

                if (wasInc && (++size == sampleSize))
                {
                    Reset();
                }
            }
        }
#endif
    }
}
