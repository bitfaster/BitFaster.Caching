using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


#if !NETSTANDARD2_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#endif

#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
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
#if NET6_0_OR_GREATER
        private long* tableAddr;
#endif
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
#if NET6_0_OR_GREATER
            else if (isa.IsArm64Supported)
            {
                return EstimateFrequencyArm(value);
            }
#endif
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
#if NET6_0_OR_GREATER
            else if (isa.IsArm64Supported)
            {
                IncrementArm(value);
            }
#endif
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
            Array.Clear(table, 0, table.Length);
            size = 0;
        }

        [MemberNotNull(nameof(table))]
        private void EnsureCapacity(long maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, int.MaxValue >> 1);

#if NET6_0_OR_GREATER
            I isa = default;
            if (isa.IsAvx2Supported || isa.IsArm64Supported)
            {
                // over alloc by 8 to give 64 bytes padding, tableAddr is then aligned to 64 bytes
                const int pad = 8;
                bool pinned = true;
                table = GC.AllocateArray<long>(Math.Max(BitOps.CeilingPowerOfTwo(maximum), 8) + pad, pinned);

                tableAddr = (long*)Unsafe.AsPointer(ref table[0]);
                tableAddr = (long*)((long)tableAddr + (long)tableAddr % 64);

                blockMask = (int)((uint)(table.Length - pad) >> 3) - 1;
            }
            else
#endif
            {
                table = new long[Math.Max(BitOps.CeilingPowerOfTwo(maximum), 8)];
                blockMask = (int)((uint)(table.Length) >> 3) - 1;
            }

            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

            size = 0;
        }

        private unsafe int EstimateFrequencyStd(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            int h0 = counterHash;
            int h1 = counterHash >>> 8;
            int h2 = counterHash >>> 16;
            int h3 = counterHash >>> 24;

            int index0 = (h0 >>> 1) & 15;
            int index1 = (h1 >>> 1) & 15;
            int index2 = (h2 >>> 1) & 15;
            int index3 = (h3 >>> 1) & 15;

            int slot0 = block + (h0 & 1);
            int slot1 = block + (h1 & 1) + 2;
            int slot2 = block + (h2 & 1) + 4;
            int slot3 = block + (h3 & 1) + 6;

            int count0 = (int)((table[slot0] >>> (index0 << 2)) & 0xfL);
            int count1 = (int)((table[slot1] >>> (index1 << 2)) & 0xfL);
            int count2 = (int)((table[slot2] >>> (index2 << 2)) & 0xfL);
            int count3 = (int)((table[slot3] >>> (index3 << 2)) & 0xfL);

            return Math.Min(Math.Min(count0, count1), Math.Min(count2, count3));
        }

        private unsafe void IncrementStd(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            // Loop unrolling improves throughput
            int h0 = counterHash;
            int h1 = counterHash >>> 8;
            int h2 = counterHash >>> 16;
            int h3 = counterHash >>> 24;

            int index0 = (h0 >>> 1) & 15;
            int index1 = (h1 >>> 1) & 15;
            int index2 = (h2 >>> 1) & 15;
            int index3 = (h3 >>> 1) & 15;

            int slot0 = block + (h0 & 1);
            int slot1 = block + (h1 & 1) + 2;
            int slot2 = block + (h2 & 1) + 4;
            int slot3 = block + (h3 & 1) + 6;

            bool added =
                  IncrementAt(slot0, index0)
                | IncrementAt(slot1, index1)
                | IncrementAt(slot2, index2)
                | IncrementAt(slot3, index3);

            if (added && (++size == sampleSize))
            {
                Reset();
            }
        }

        // Applies another round of hashing for additional randomization.
        private static int Rehash(int x)
        {
            x = (int)(x * 0x31848bab);
            x ^= (int)((uint)x >> 14);
            return x;
        }

        // Applies a supplemental hash function to defend against poor quality hash.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int EstimateFrequencyAvx(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = Avx2.ShiftRightLogicalVariable(Vector128.Create(counterHash).AsUInt32(), Vector128.Create(0U, 8U, 16U, 24U)).AsInt32();
            Vector128<int> index = Avx2.ShiftLeftLogical(Avx2.And(Avx2.ShiftRightLogical(h, 1), Vector128.Create(15)), 2);
            Vector128<int> blockOffset = Avx2.Add(Avx2.Add(Vector128.Create(block), Avx2.And(h, Vector128.Create(1))), Vector128.Create(0, 2, 4, 6));

            Vector256<ulong> indexLong = Avx2.PermuteVar8x32(Vector256.Create(index, Vector128<int>.Zero), Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7)).AsUInt64();

#if NET6_0_OR_GREATER
            long* tablePtr = tableAddr;
#else
            fixed (long* tablePtr = table)
#endif
            {
                Vector128<ushort> count = Avx2.PermuteVar8x32(Avx2.And(Avx2.ShiftRightLogicalVariable(Avx2.GatherVector256(tablePtr, blockOffset, 8), indexLong), Vector256.Create(0xfL)).AsInt32(), Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7))
                    .GetLower()
                    .AsUInt16();

                // set the zeroed high parts of the long value to ushort.Max
#if NET6_0_OR_GREATER
                count = Avx2.Blend(count, Vector128<ushort>.AllBitsSet, 0b10101010);
#else
                count = Avx2.Blend(count, Vector128.Create(ushort.MaxValue), 0b10101010);
#endif

                return Avx2.MinHorizontal(count).GetElement(0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void IncrementAvx(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = Avx2.ShiftRightLogicalVariable(Vector128.Create(counterHash).AsUInt32(), Vector128.Create(0U, 8U, 16U, 24U)).AsInt32();
            Vector128<int> index = Avx2.ShiftLeftLogical(Avx2.And(Avx2.ShiftRightLogical(h, 1), Vector128.Create(15)), 2);
            Vector128<int> blockOffset = Avx2.Add(Avx2.Add(Vector128.Create(block), Avx2.And(h, Vector128.Create(1))), Vector128.Create(0, 2, 4, 6));

            Vector256<ulong> offsetLong = Avx2.PermuteVar8x32(Vector256.Create(index, Vector128<int>.Zero), Vector256.Create(0, 4, 1, 5, 2, 5, 3, 7)).AsUInt64();
            Vector256<long> mask = Avx2.ShiftLeftLogicalVariable(Vector256.Create(0xfL), offsetLong);

#if NET6_0_OR_GREATER
            long* tablePtr = tableAddr;
#else
            fixed (long* tablePtr = table)
#endif
            {
                // Note masked is 'equal' - therefore use AndNot below
                Vector256<long> masked = Avx2.CompareEqual(Avx2.And(Avx2.GatherVector256(tablePtr, blockOffset, 8), mask), mask);

                // Mask to zero out non matches (add zero below) - first operand is NOT then AND result (order matters)
                Vector256<long> inc = Avx2.AndNot(masked, Avx2.ShiftLeftLogicalVariable(Vector256.Create(1L), offsetLong));

                bool wasInc = Avx2.MoveMask(Avx2.CompareEqual(masked.AsByte(), Vector256<byte>.Zero).AsByte()) == unchecked((int)(0b1111_1111_1111_1111_1111_1111_1111_1111));

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

#if NET6_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void IncrementArm(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = AdvSimd.ShiftArithmetic(Vector128.Create(counterHash), Vector128.Create(0, -8, -16, -24));
            Vector128<int> index = AdvSimd.And(AdvSimd.ShiftRightLogical(h, 1), Vector128.Create(0xf));
            Vector128<int> blockOffset = AdvSimd.Add(AdvSimd.Add(Vector128.Create(block), AdvSimd.And(h, Vector128.Create(1))), Vector128.Create(0, 2, 4, 6));

            long* tablePtr = tableAddr;
            {
                int t0 = AdvSimd.Extract(blockOffset, 0);
                int t1 = AdvSimd.Extract(blockOffset, 1);
                int t2 = AdvSimd.Extract(blockOffset, 2);
                int t3 = AdvSimd.Extract(blockOffset, 3);

                Vector128<long> tableVectorA = Vector128.Create(AdvSimd.LoadVector64(tablePtr + t0), AdvSimd.LoadVector64(tablePtr + t1));
                Vector128<long> tableVectorB = Vector128.Create(AdvSimd.LoadVector64(tablePtr + t2), AdvSimd.LoadVector64(tablePtr + t3));

                index = AdvSimd.ShiftLeftLogicalSaturate(index, 2);

                Vector128<int> longOffA = AdvSimd.Arm64.InsertSelectedScalar(AdvSimd.Arm64.InsertSelectedScalar(Vector128<int>.Zero, 0, index, 0), 2, index, 1);
                Vector128<int> longOffB = AdvSimd.Arm64.InsertSelectedScalar(AdvSimd.Arm64.InsertSelectedScalar(Vector128<int>.Zero, 0, index, 2), 2, index, 3);

                Vector128<long> fifteen = Vector128.Create(0xfL);
                Vector128<long> maskA = AdvSimd.ShiftArithmetic(fifteen, longOffA.AsInt64());
                Vector128<long> maskB = AdvSimd.ShiftArithmetic(fifteen, longOffB.AsInt64());

                Vector128<long> maskedA = AdvSimd.Not(AdvSimd.Arm64.CompareEqual(AdvSimd.And(tableVectorA, maskA), maskA));
                Vector128<long> maskedB = AdvSimd.Not(AdvSimd.Arm64.CompareEqual(AdvSimd.And(tableVectorB, maskB), maskB));

                var one = Vector128.Create(1L);
                Vector128<long> incA = AdvSimd.And(maskedA, AdvSimd.ShiftArithmetic(one, longOffA.AsInt64()));
                Vector128<long> incB = AdvSimd.And(maskedB, AdvSimd.ShiftArithmetic(one, longOffB.AsInt64()));

                tablePtr[t0] += AdvSimd.Extract(incA, 0);
                tablePtr[t1] += AdvSimd.Extract(incA, 1);
                tablePtr[t2] += AdvSimd.Extract(incB, 0);
                tablePtr[t3] += AdvSimd.Extract(incB, 1);

                var max = AdvSimd.Arm64.MaxAcross(AdvSimd.Arm64.InsertSelectedScalar(AdvSimd.Arm64.MaxAcross(incA.AsInt32()), 1, AdvSimd.Arm64.MaxAcross(incB.AsInt32()), 0).AsInt16());

                if (max.ToScalar() != 0 && (++size == sampleSize))
                {
                    Reset();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int EstimateFrequencyArm(T value)
        {
            int blockHash = Spread(comparer.GetHashCode(value));
            int counterHash = Rehash(blockHash);
            int block = (blockHash & blockMask) << 3;

            Vector128<int> h = AdvSimd.ShiftArithmetic(Vector128.Create(counterHash), Vector128.Create(0, -8, -16, -24));
            Vector128<int> index = AdvSimd.And(AdvSimd.ShiftRightLogical(h, 1), Vector128.Create(0xf));
            Vector128<int> blockOffset = AdvSimd.Add(AdvSimd.Add(Vector128.Create(block), AdvSimd.And(h, Vector128.Create(1))), Vector128.Create(0, 2, 4, 6));

            long* tablePtr = tableAddr;
            {
                Vector128<long> tableVectorA = Vector128.Create(AdvSimd.LoadVector64(tablePtr + AdvSimd.Extract(blockOffset, 0)), AdvSimd.LoadVector64(tablePtr + AdvSimd.Extract(blockOffset, 1)));
                Vector128<long> tableVectorB = Vector128.Create(AdvSimd.LoadVector64(tablePtr + AdvSimd.Extract(blockOffset, 2)), AdvSimd.LoadVector64(tablePtr + AdvSimd.Extract(blockOffset, 3)));

                index = AdvSimd.ShiftLeftLogicalSaturate(index, 2);

                Vector128<int> indexA = AdvSimd.Negate(AdvSimd.Arm64.InsertSelectedScalar(AdvSimd.Arm64.InsertSelectedScalar(Vector128<int>.Zero, 0, index, 0), 2, index, 1));
                Vector128<int> indexB = AdvSimd.Negate(AdvSimd.Arm64.InsertSelectedScalar(AdvSimd.Arm64.InsertSelectedScalar(Vector128<int>.Zero, 0, index, 2), 2, index, 3));

                var fifteen = Vector128.Create(0xfL);
                Vector128<long> a = AdvSimd.And(AdvSimd.ShiftArithmetic(tableVectorA, indexA.AsInt64()), fifteen);
                Vector128<long> b = AdvSimd.And(AdvSimd.ShiftArithmetic(tableVectorB, indexB.AsInt64()), fifteen);

                // Before: < 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, A, B, C, D, E, F >
                // After:  < 0, 1, 2, 3, 8, 9, A, B, 4, 5, 6, 7, C, D, E, F >
                var min = AdvSimd.Arm64.VectorTableLookup(a.AsByte(), Vector128.Create(0x0B0A090803020100, 0xFFFFFFFFFFFFFFFF).AsByte());
                min = AdvSimd.Arm64.VectorTableLookupExtension(min, b.AsByte(), Vector128.Create(0xFFFFFFFFFFFFFFFF, 0x0B0A090803020100).AsByte());

                var min32 = AdvSimd.Arm64.MinAcross(min.AsInt32());

                return min32.ToScalar();
            }
        }
#endif
    }
}
