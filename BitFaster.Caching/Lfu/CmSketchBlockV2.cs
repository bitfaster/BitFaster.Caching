using System;
using System.Collections.Generic;

#if !NETSTANDARD2_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Hash into blocks that fit within CPU cache lines.
    /// </summary>
    public class CmSketchBlockV2<T, I> where I : struct, IsaProbe
    {
        private static readonly ulong[] Seed = { 0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L };
        private static readonly long ResetMask = 0x7777777777777777L;
        private static readonly long OneMask = 0x1111111111111111L;

        private int sampleSize;
        private long[] table;
        private int size;

        int blockMask;

        private readonly IEqualityComparer<T> comparer;

        /// <summary>
        /// Initializes a new instance of the CmSketch class with the specified maximum size and equality comparer.
        /// </summary>
        /// <param name="maximumSize">The maximum size.</param>
        /// <param name="comparer">The equality comparer.</param>
        public CmSketchBlockV2(long maximumSize, IEqualityComparer<T> comparer)
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
        /// Estimate the frequency of the specified value.
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
        /// Estimate the frequency of the specified values.
        /// </summary>
        /// <param name="value1">The first value</param>
        /// <param name="value2">The second value</param>
        /// <returns>The estimated frequency of the values.</returns>
//        public (int, int) EstimateFrequency(T value1, T value2)
//        {
//#if NETSTANDARD2_0
//            return (EstimateFrequencyStd(value1), EstimateFrequencyStd(value2));
//#else

//            I isa = default;

//            if (isa.IsAvx2Supported)
//            {
//                return EstimateFrequencyAvx(value1, value2);
//            }
//            else
//            {
//                return (EstimateFrequencyStd(value1), EstimateFrequencyStd(value2));
//            }
//#endif
//        }

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
            table = new long[table.Length];
            size = 0;
        }

        private int EstimateFrequencyStd(T value)
        {
            int baseHash = BaseHash(comparer.GetHashCode(value));
            int blockHash = BlockHash(baseHash);
            int counterHash = CounterHash(blockHash);
            int block = (blockHash & blockMask) << 3;

            int frequency = int.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                int h = counterHash << (i << 3);
                int offset = h & 1;
                int index = (h >> 1) & 15;
                int count = (int)(((ulong)table[block + offset + (i << 1)] >> (index << 2)) & 0xfL);
                frequency = Math.Min(frequency, count);
            }

            return frequency;
        }

        private void IncrementStd(T value)
        {
            int baseHash = BaseHash(comparer.GetHashCode(value));
            int blockHash = BlockHash(baseHash);
            int counterHash = CounterHash(blockHash);
            int block = (blockHash & blockMask) << 3;

            bool added = false;
            for (int i = 0; i < 4; i++)
            {
                int h = counterHash << (i << 3);
                int offset = h & 1;
                int index = (h >> 1) & 15;
                added |= IncrementAt(block + offset + (i << 1), index);
            }

            if (added && (++size == sampleSize))
            {
                Reset();
            }
        }

        private void EnsureCapacity(long maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, int.MaxValue >> 1);

            table = new long[(maximum == 0) ? 1 : BitOps.CeilingPowerOfTwo(maximum)];
            blockMask = (int)Math.Max(0, ((uint)table.Length >> 3) - 1);
            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

            size = 0;
        }

        bool IncrementAt(int i, int j)
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

        int BaseHash(int x)
        {
            uint ux = (uint)x;
            ux = ((ux >> 16) ^ ux) * 0x45d9f3b;
            ux = ((ux >> 16) ^ ux) * 0x45d9f3b;
            return (int)ux;
        }

        int BlockHash(int x)
        {
            return (int)(((uint)x >> 16) ^ (uint)x);
        }

        int CounterHash(int x)
        {
            uint ux = (uint)x;
            ux = ((ux >> 16) ^ ux) * 0x45d9f3b;
            return (int)((ux >> 16) ^ ux);
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
#if !NETSTANDARD2_0
        private unsafe int EstimateFrequencyAvx(T value)
        {
            int baseHash = BaseHash(comparer.GetHashCode(value));
            int blockHash = BlockHash(baseHash);
            int counterHash = CounterHash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = Vector128.Create(counterHash);
            h = Avx2.ShiftLeftLogical(h, Vector128.Create(0, 8, 16, 24));

            Vector128<int> offset = Avx2.And(h, Vector128.Create(1));
            var index = Avx2.ShiftRightLogical(h, 1);
            index = Avx2.And(index, Vector128.Create(15));

            var blockIndex = Vector128.Create(block);
            blockIndex = Avx2.Add(blockIndex, offset);
            blockIndex = Avx2.Add(blockIndex, Vector128.Create(0, 2, 4, 6)); // (i << 1)

            fixed (long* tablePtr = &table[0])
            {
                var tableVector = Avx2.GatherVector256(tablePtr, blockIndex, 8);
                index = Avx2.ShiftLeftLogical(index, 2);

                // convert index from int to long via permute
                Vector256<long> indexLong = Vector256.Create(index, Vector128.Create(0)).AsInt64();
                Vector256<int> permuteMask2 = Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7);
                indexLong = Avx2.PermuteVar8x32(indexLong.AsInt32(), permuteMask2).AsInt64();
                tableVector = Avx2.ShiftRightLogicalVariable(tableVector, indexLong.AsUInt64());
                tableVector = Avx2.And(tableVector, Vector256.Create(0xfL));

                Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
                Vector128<ushort> count = Avx2.PermuteVar8x32(tableVector.AsInt32(), permuteMask)
                    .GetLower()
                    .AsUInt16();

                // set the zeroed high parts of the long value to ushort.Max
                count = Avx2.Blend(count, Vector128.Create(ushort.MaxValue), 0b10101010);
                return Avx2.MinHorizontal(count).GetElement(0);
            }
        }

        private unsafe void IncrementAvx(T value)
        {
            int baseHash = BaseHash(comparer.GetHashCode(value));
            int blockHash = BlockHash(baseHash);
            int counterHash = CounterHash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = Vector128.Create(counterHash);
            h = Avx2.ShiftLeftLogical(h, Vector128.Create(0, 8, 16, 24));

            Vector128<int> offset = Avx2.And(h, Vector128.Create(1));
            var index = Avx2.ShiftRightLogical(h, 1);
            index = Avx2.And(index, Vector128.Create(15));

            var blockIndex = Vector128.Create(block);
            blockIndex = Avx2.Add(blockIndex, offset);
            blockIndex = Avx2.Add(blockIndex, Vector128.Create(0, 2, 4, 6)); // (i << 1)

            fixed (long* tablePtr = &table[0])
            {
                var tableVector = Avx2.GatherVector256(tablePtr, blockIndex, 8);

                // j == index
                Vector256<long> offsetLong = Vector256.Create(offset, Vector128.Create(0)).AsInt64();

                Vector256<int> permuteMask2 = Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7);
                var fifteen2 = Vector256.Create(0xfL);
                offsetLong = Avx2.PermuteVar8x32(offsetLong.AsInt32(), permuteMask2).AsInt64();

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

                *(tablePtr + blockIndex.GetElement(0)) += inc.GetElement(0);
                *(tablePtr + blockIndex.GetElement(1)) += inc.GetElement(1);
                *(tablePtr + blockIndex.GetElement(2)) += inc.GetElement(2);
                *(tablePtr + blockIndex.GetElement(3)) += inc.GetElement(3);

                Vector256<byte> result = Avx2.CompareEqual(masked.AsByte(), Vector256.Create(0).AsByte());
                bool wasInc = Avx2.MoveMask(result.AsByte()) == unchecked((int)(0b1111_1111_1111_1111_1111_1111_1111_1111));

                if (wasInc && (++size == sampleSize))
                {
                    Reset();
                }
            }
        }
#endif
    }
}
