using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    [Collection("Soak")]
    public class ConcurrentLfuSoakTests
    {
        private readonly ITestOutputHelper output;
        public ConcurrentLfuSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Fact]
        public async Task ThreadedVerifyMisses()
        {
            // buffer size is 1, this will cause dropped writes on some threads where the buffer is full
            var cache = new ConcurrentLfu<int, int>(1, 20, new NullScheduler(), EqualityComparer<int>.Default);

            int threads = 4;
            int iterations = 100_000;

            await Threaded.Run(threads, i =>
            {
                Func<int, int> func = x => x;

                int start = i * iterations;

                for (int j = start; j < start + iterations; j++)
                {
                    cache.GetOrAdd(j, func);
                }
            });

            var samplePercent = cache.Metrics.Value.Misses / (double)iterations / threads * 100;

            this.output.WriteLine($"Cache misses {cache.Metrics.Value.Misses} (sampled {samplePercent}%)");
            this.output.WriteLine($"Maintenance ops {cache.Scheduler.RunCount}");

            cache.Metrics.Value.Misses.Should().Be(iterations * threads);
        }
    }
}
