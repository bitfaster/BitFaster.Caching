using System;
using System.Collections.Generic;

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
    /// This is a direct C# translation of FrequencySketch in the Caffeine library by ben.manes@gmail.com (Ben Manes).
    /// https://github.com/ben-manes/caffeine
    public sealed class CmSketch<T, I> where I : struct, IsaProbe
    {
        // A mixture of seeds from FNV-1a, CityHash, and Murmur3
        private static readonly ulong[] Seed = { 0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L};
        private static readonly long ResetMask = 0x7777777777777777L;
        private static readonly long OneMask = 0x1111111111111111L;

        private int sampleSize;
        private int tableMask;
        private long[] table;
        private int size;

        private readonly IEqualityComparer<T> comparer;

        /// <summary>
        /// Initializes a new instance of the CmSketch class with the specified maximum size and equality comparer.
        /// </summary>
        /// <param name="maximumSize">The maximum size.</param>
        /// <param name="comparer">The equality comparer.</param>
        public CmSketch(long maximumSize, IEqualityComparer<T> comparer)
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

        private void IncrementStd(T value)
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

        private void EnsureCapacity(long maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, int.MaxValue >> 1);

            // clamp to 4 as min size
            maximum = Math.Max(4, maximum);

            table = new long[(maximum == 0) ? 1 : BitOps.CeilingPowerOfTwo(maximum)];
            tableMask = Math.Max(0, table.Length - 1);
            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

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

#if !NETSTANDARD2_0
        private unsafe int EstimateFrequencyAvx(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));
            int start = (hash & 3) << 2;

            fixed (long* tablePtr = &table[0])
            {
                var tableVector = Avx2.GatherVector256(tablePtr, IndexesOfAvx(hash), 8).AsUInt64();

                Vector256<ulong> starts = Vector256.Create(0UL, 1UL, 2UL, 3UL);
                starts = Avx2.Add(starts, Vector256.Create((ulong)start));
                starts = Avx2.ShiftLeftLogical(starts, 2);

                tableVector = Avx2.ShiftRightLogicalVariable(tableVector, starts);
                tableVector = Avx2.And(tableVector, Vector256.Create(0xfUL));

                Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
                Vector128<ushort> lower = Avx2.PermuteVar8x32(tableVector.AsInt32(), permuteMask)
                        .GetLower()
                        .AsUInt16();

                // set the zeroed high parts of the long value to ushort.Max
                var masked = Avx2.Blend(lower, Vector128.Create(ushort.MaxValue), 0b10101010);
                return Avx2.MinHorizontal(masked).GetElement(0);
            }
        }

        private unsafe void IncrementAvx(T value)
        {
            int hash = Spread(comparer.GetHashCode(value));
            int start = (hash & 3) << 2;

            Vector128<int> indexes = IndexesOfAvx(hash);

            fixed (long* tablePtr = &table[0])
            {
                var tableVector = Avx2.GatherVector256(tablePtr, indexes, 8);

                // offset = j << 2, where j [start+0, start+1, start+2, start+3]
                Vector256<ulong> offset = Vector256.Create((ulong)start);
                Vector256<ulong> add = Vector256.Create(0UL, 1UL, 2UL, 3UL);
                offset = Avx2.Add(offset, add);
                offset = Avx2.ShiftLeftLogical(offset, 2);

                // mask = (0xfL << offset)
                Vector256<long> fifteen = Vector256.Create(0xfL);
                Vector256<long> mask = Avx2.ShiftLeftLogicalVariable(fifteen, offset);

                // (table[i] & mask) != mask)
                // Note masked is 'equal' - therefore use AndNot below
                Vector256<long> masked = Avx2.CompareEqual(Avx2.And(tableVector, mask), mask);

                // 1L << offset
                Vector256<long> inc = Avx2.ShiftLeftLogicalVariable(Vector256.Create(1L), offset);

                // Mask to zero out non matches (add zero below) - first operand is NOT then AND result (order matters)
                inc = Avx2.AndNot(masked, inc);

                *(tablePtr + indexes.GetElement(0)) += inc.GetElement(0);
                *(tablePtr + indexes.GetElement(1)) += inc.GetElement(1);
                *(tablePtr + indexes.GetElement(2)) += inc.GetElement(2);
                *(tablePtr + indexes.GetElement(3)) += inc.GetElement(3);

                Vector256<byte> result = Avx2.CompareEqual(masked.AsByte(), Vector256.Create(0).AsByte());
                bool wasInc = Avx2.MoveMask(result.AsByte()) == unchecked((int)(0b1111_1111_1111_1111_1111_1111_1111_1111));

                if (wasInc && (++size == sampleSize))
                {
                    Reset();
                }
            }
        }

        private Vector128<int> IndexesOfAvx(int item)
        {
            Vector256<ulong> VectorSeed = Vector256.Create(0xc3a5c85c97cb3127L, 0xb492b66fbe98f273L, 0x9ae16a3b2f90404fL, 0xcbf29ce484222325L);
            Vector256<ulong> hash = Vector256.Create((ulong)item);
            hash = Avx2.Add(hash, VectorSeed);
            hash = Multiply(hash, VectorSeed);

            Vector256<ulong> shift = Vector256.Create(32UL);
            Vector256<ulong> shifted = Avx2.ShiftRightLogicalVariable(hash, shift);
            hash = Avx2.Add(hash, shifted);

            // Move            [a1, a2, b1, b2, c1, c2, d1, d2]
            // To              [a1, b1, c1, d1, a2, b2, c2, d2]
            // then GetLower() [a1, b1, c1, d1]
            Vector256<int> permuteMask = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
            Vector128<int> f = Avx2.PermuteVar8x32(hash.AsInt32(), permuteMask)
                .GetLower();

            Vector128<int> maskVector = Vector128.Create(tableMask);
            return Avx2.And(f, maskVector);
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
