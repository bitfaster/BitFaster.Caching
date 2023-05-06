
using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Buffers;

namespace BitFaster.Caching.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [DisassemblyDiagnoser(printSource: true, maxDepth: 3)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class DrainBenchmarks
    {
        private const int bufferSize = 128;
        private readonly MpscBoundedBuffer<string> buffer = new MpscBoundedBuffer<string>(bufferSize);

        private readonly string[] output = new string[bufferSize];

        //[Benchmark(Baseline = true)]
        public void Add()
        {
            // 8
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 16
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 24
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 32
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 40
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 48
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 56
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 64
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 72
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 80
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 88
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 96
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 104
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 112
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 120
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);

            // 128
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
            buffer.TryAdd(string.Empty);
        }

        [Benchmark(Baseline = true)]
        public void DrainArray()
        {
            Add();
#if NETCOREAPP3_1_OR_GREATER
            buffer.DrainTo(output.AsSpan());
#else
            buffer.DrainTo(new ArraySegment<string>(output));
#endif
        }
    }
}
