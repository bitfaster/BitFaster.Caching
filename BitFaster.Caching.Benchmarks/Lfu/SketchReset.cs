
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net60)]
    public class Reset
    {
        static long ResetMask = 0x7777777777777777L;
        static long OneMask = 0x1111111111111111L;

        long[] table;

        [Params(4, 128, 8192, 1048576)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            table = new long[Size];
        }

        [Benchmark(Baseline = true)]
        public int Reset1()
        {
            int count = 0;
            for (int i = 0; i < table.Length; i++)
            {
                count += BitOps.BitCount(table[i] & OneMask);
                table[i] = (long)((ulong)table[i] >> 1) & ResetMask;
            }

            return count;
        }

        [Benchmark()]
        public int Reset2()
        {
            int count0 = 0;
            int count1 = 0;

            for (int i = 0; i < table.Length; i += 2)
            {
                count0 += BitOps.BitCount(table[i] & OneMask);
                count1 += BitOps.BitCount(table[i + 1] & OneMask);

                table[i] = (long)((ulong)table[i] >> 1) & ResetMask;
                table[i + 1] = (long)((ulong)table[i + 1] >> 1) & ResetMask;
            }

            return count0 + count1;
        }

        [Benchmark()]
        public int Reset4()
        {
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

            return (count0 + count1) + (count2 + count3);
        }
    }
}
