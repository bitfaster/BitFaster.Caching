using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace BitFaster.Caching.UnitTests
{
    public class Alignment
    {
        static long ResetMask = 0x7777777777777777L;

        long[] table;

        private const ulong AlignmentMask = 31UL;

        [Fact]
        public void Runner()
        {
            for (int i = 0; i < 8000; i++)
            {
                Test();
            }
        }

        private unsafe void Test()
        {
            table = new long[128];

            for (int i = 0; i < table.Length; i++)
            {
                table[i] = 15;
            }


            var resetMaskVector = Vector256.Create(ResetMask);

            fixed (long* tPtr = &table[0])
            {
                long* alignedPtr = tPtr;
                int remainder = 0;

                while (((ulong)alignedPtr & 31UL) != 0)
                {
                    *alignedPtr = (*alignedPtr >> 1) & ResetMask;
                    alignedPtr++;
                    remainder = 16;
                }

                int c = table.Length - (int)(alignedPtr - tPtr) - remainder;
                int i = 0;

                for (; i < c; i += 16)
                {
                    Vector256<long> t1 = Avx2.LoadAlignedVector256(alignedPtr + i).AsInt64();
                    t1 = Avx2.ShiftRightLogical(t1, 1);
                    t1 = Avx2.And(t1, resetMaskVector);
                    Avx2.StoreAligned(alignedPtr + i, t1);

                    Vector256<long> t2 = Avx2.LoadAlignedVector256(alignedPtr + i + 4).AsInt64();
                    t2 = Avx2.ShiftRightLogical(t2, 1);
                    t2 = Avx2.And(t2, resetMaskVector);
                    Avx2.StoreAligned(alignedPtr + i + 4, t2);

                    Vector256<long> t3 = Avx2.LoadAlignedVector256(alignedPtr + i + 8).AsInt64();
                    t3 = Avx2.ShiftRightLogical(t3, 1);
                    t3 = Avx2.And(t3, resetMaskVector);
                    Avx2.StoreAligned(alignedPtr + i + 8, t3);

                    Vector256<long> t4 = Avx2.LoadAlignedVector256(alignedPtr + i + 12).AsInt64();
                    t4 = Avx2.ShiftRightLogical(t4, 1);
                    t4 = Avx2.And(t4, resetMaskVector);
                    Avx2.StoreAligned(alignedPtr + i + 12, t4);
                }

                int start = (int)(alignedPtr - tPtr) + i;

                for (int j = start; j < table.Length; j++)
                {
                    tPtr[j] = (tPtr[j] >> 1) & ResetMask;
                }

                for (int j = 0; j < table.Length; j++)
                {
                    table[j].Should().Be(7);
                }
            }
        }
    }
}
