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
    public class CmSketchBlock<T, I> where I : struct, IsaProbe
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
        public CmSketchBlock(long maximumSize, IEqualityComparer<T> comparer)
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
        public (int, int) EstimateFrequency(T value1, T value2)
        {
#if NETSTANDARD2_0
            return (EstimateFrequencyStd(value1), EstimateFrequencyStd(value2));
#else

            I isa = default;

            if (isa.IsAvx2Supported)
            {
                return EstimateFrequencyAvx(value1, value2);
            }
            else
            {
                return (EstimateFrequencyStd(value1), EstimateFrequencyStd(value2));
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
            table = new long[table.Length];
            size = 0;
        }

        private int EstimateFrequencyStd(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));

            int block = (hash & blockMask) << 3;
            int frequency = int.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                int h = Rehash(hash, i);
                int offset = h & 1;
                int index = (h >> 1) & 15;
                int count = (int)(((ulong)table[block + offset + (i << 1)] >> (index << 2)) & 0xfL);
                frequency = Math.Min(frequency, count);
            }

            return frequency;
        }

        private void IncrementStd(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));
            int block = (hash & blockMask) << 3;

            bool added = false;
            for (int i = 0; i < 4; i++)
            {
                int h = Rehash(hash, i);
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

        int Rehash(int item, int i)
        {
            ulong hash = ((ulong)item + Seed[i]) * Seed[i];
            hash += (hash >> 32);
            return ((int)hash);
        }

        private int Spread(int x)
        {
            uint y = (uint)x;
            y = ((y >> 16) ^ y) * 0x45d9f3b;
            y = ((y >> 16) ^ y) * 0x45d9f3b;
            return (int)((y >> 16) ^ y);
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
            int hash = Spread(comparer.GetHashCode(value));
            int block = (hash & blockMask) << 3;

            // rehash
            Vector256<ulong> VectorSeed = Vector256.Create(0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L);
            Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
            Vector128<int> rehashInt = Rehash(ref permuteMask, ref VectorSeed, hash);

            Vector128<int> offset = Avx2.And(rehashInt, Vector128.Create(1));
            var index = Avx2.ShiftRightLogical(rehashInt, 1);
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

                Vector128<ushort> lower = Avx2.PermuteVar8x32(tableVector.AsInt32(), permuteMask)
                    .GetLower()
                    .AsUInt16();

                // set the zeroed high parts of the long value to ushort.Max
                var masked = Avx2.Blend(lower, Vector128.Create(ushort.MaxValue), 0b10101010);
                return Avx2.MinHorizontal(masked).GetElement(0);
            }
        }

        private static Vector128<int> Rehash(ref Vector256<int> permuteMask, ref Vector256<ulong> VectorSeed, int hash)
        {
            Vector256<ulong> rehash = Vector256.Create((ulong)hash);
            rehash = Avx2.Add(rehash, VectorSeed);
            rehash = Multiply(rehash, VectorSeed);
            rehash = Avx2.Add(rehash, Avx2.ShiftRightLogical(rehash, 32));

            return Avx2.PermuteVar8x32(rehash.AsInt32(), permuteMask)
                .GetLower();
        }

        private unsafe (int, int) EstimateFrequencyAvx(T value1, T value2)
        {
            int hash1 = Spread(comparer.GetHashCode(value1));
            int block1 = (hash1 & blockMask) << 3;

            int hash2 = Spread(comparer.GetHashCode(value2));
            int block2 = (hash2 & blockMask) << 3;

            // rehash
            Vector256<ulong> VectorSeed = Vector256.Create(0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L);
            Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);

            Vector128<int> rehash1 = Rehash(ref permuteMask, ref VectorSeed, hash1);
            Vector128<int> rehash2 = Rehash(ref permuteMask, ref VectorSeed, hash2);

            Vector128<int> one = Vector128.Create(1);
            Vector128<int> offset1 = Avx2.And(rehash1, one);
            Vector128<int> offset2 = Avx2.And(rehash2, one);

            Vector128<int> fifteen = Vector128.Create(15);
            var index1 = Avx2.ShiftRightLogical(rehash1, 1);
            index1 = Avx2.And(index1, fifteen);
            var index2 = Avx2.ShiftRightLogical(rehash2, 1);
            index2 = Avx2.And(index2, fifteen);

            var inc = Vector128.Create(0, 2, 4, 6);// (i << 1)
            var blockIndex1 = Vector128.Create(block1);
            blockIndex1 = Avx2.Add(blockIndex1, offset1);
            blockIndex1 = Avx2.Add(blockIndex1, inc);
            var blockIndex2 = Vector128.Create(block2);
            blockIndex2 = Avx2.Add(blockIndex2, offset2);
            blockIndex2 = Avx2.Add(blockIndex2, inc);

            fixed (long* tablePtr = &table[0])
            {
                var tableVector1 = Avx2.GatherVector256(tablePtr, blockIndex1, 8);
                index1 = Avx2.ShiftLeftLogical(index1, 2);

                var tableVector2 = Avx2.GatherVector256(tablePtr, blockIndex2, 8);
                index2 = Avx2.ShiftLeftLogical(index2, 2);

                // convert index from int to long via permute
                Vector256<long> indexLong1 = Vector256.Create(index1, Vector128.Create(0)).AsInt64();
                Vector256<long> indexLong2 = Vector256.Create(index2, Vector128.Create(0)).AsInt64();

                Vector256<int> permuteMask2 = Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7);
                var fifteen2 = Vector256.Create(0xfL);
                indexLong1 = Avx2.PermuteVar8x32(indexLong1.AsInt32(), permuteMask2).AsInt64();
                tableVector1 = Avx2.ShiftRightLogicalVariable(tableVector1, indexLong1.AsUInt64());
                tableVector1 = Avx2.And(tableVector1, fifteen2);

                indexLong2 = Avx2.PermuteVar8x32(indexLong2.AsInt32(), permuteMask2).AsInt64();
                tableVector2 = Avx2.ShiftRightLogicalVariable(tableVector2, indexLong2.AsUInt64());
                tableVector2 = Avx2.And(tableVector2, fifteen2);

                Vector128<ushort> lower1 = Avx2.PermuteVar8x32(tableVector1.AsInt32(), permuteMask)
                    .GetLower()
                    .AsUInt16();

                Vector128<ushort> lower2 = Avx2.PermuteVar8x32(tableVector2.AsInt32(), permuteMask)
                    .GetLower()
                    .AsUInt16();

                // set the zeroed high parts of the long value to ushort.Max
                var masked1 = Avx2.Blend(lower1, Vector128.Create(ushort.MaxValue), 0b10101010);
                var masked2 = Avx2.Blend(lower2, Vector128.Create(ushort.MaxValue), 0b10101010);
                return (Avx2.MinHorizontal(masked1).GetElement(0), Avx2.MinHorizontal(masked2).GetElement(0));
            }
        }

        private unsafe void IncrementAvx(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));
            int block = (hash & blockMask) << 3;

            // rehash
            Vector256<ulong> VectorSeed = Vector256.Create(0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L);
            Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);

            Vector128<int> rehash = Rehash(ref permuteMask, ref VectorSeed, hash);

            Vector128<int> boffset = Avx2.And(rehash, Vector128.Create(1));
            var index = Avx2.ShiftRightLogical(rehash, 1);
            index = Avx2.And(index, Vector128.Create(15));

            var blockIndex = Vector128.Create(block);
            blockIndex = Avx2.Add(blockIndex, boffset);
            blockIndex = Avx2.Add(blockIndex, Vector128.Create(0, 2, 4, 6)); // (i << 1)

            fixed (long* tablePtr = &table[0])
            {
                var tableVector = Avx2.GatherVector256(tablePtr, blockIndex, 8);

                // j == index
                var offset = Avx2.ShiftLeftLogical(index, 2);
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

        // taken from Agner Fog's vector library, see https://github.com/vectorclass/version2, vectori256.h
        private static Vector256<ulong> Multiply(Vector256<ulong> a, Vector256<ulong> b)
        {
            // instruction does not exist. Split into 32-bit multiplies
            Vector256<int> bswap = Avx2.Shuffle(b.AsInt32(), 0xB1);                 // swap H<->L
            Vector256<int> prodlh = Avx2.MultiplyLow(a.AsInt32(), bswap);           // 32 bit L*H products
            Vector256<int> zero = Vector256.Create(0);                              // 0
            Vector256<int> prodlh2 = Avx2.HorizontalAdd(prodlh, zero);              // a0Lb0H+a0Hb0L,a1Lb1H+a1Hb1L,0,0
            Vector256<int> prodlh3 = Avx2.Shuffle(prodlh2, 0x73);                   // 0, a0Lb0H+a0Hb0L, 0, a1Lb1H+a1Hb1L
            Vector256<ulong> prodll = Avx2.Multiply(a.AsUInt32(), b.AsUInt32());    // a0Lb0L,a1Lb1L, 64 bit unsigned products
            return Avx2.Add(prodll.AsInt64(), prodlh3.AsInt64()).AsUInt64();        // a0Lb0L+(a0Lb0H+a0Hb0L)<<32, a1Lb1L+(a1Lb1H+a1Hb1L)<<32
        }
#endif
    }
}
