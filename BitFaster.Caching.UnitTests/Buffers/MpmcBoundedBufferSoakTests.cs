using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Buffers
{
    [Collection("Soak")]
    public class MpmcBoundedBufferSoakTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly MpmcBoundedBuffer<string> buffer = new MpmcBoundedBuffer<string>(1024);

        public MpmcBoundedBufferSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenAddIsContendedBufferCanBeFilled(int iteration)
        {
            this.testOutputHelper.WriteLine($"Iteration {iteration}");
            
            await Threaded.Run(4, () =>
            {
                while (buffer.TryAdd("hello") != BufferStatus.Full)
                {
                }

                buffer.Count.Should().Be(1024);
            });
        }

        [Fact]
        public async Task WhileBufferIsFilledItemsCanBeTaken()
        {
            this.testOutputHelper.WriteLine($"ProcessorCount={Environment.ProcessorCount}.");

            var fill = CreateParallelFill(buffer, threads: 4, itemsPerThread: 256);

            var take = Threaded.Run(4, () =>
            {
                var spin = new SpinWait();
                int count = 0;
                while (count < 256)
                {
                    while (true)
                    {
                        if (buffer.TryTake(out _) == BufferStatus.Success)
                        {
                            break;
                        }
                        spin.SpinOnce();
                    }
                    count++;
                }
            });

            await fill.TimeoutAfter(Timeout, "fill timed out");
            await take.TimeoutAfter(Timeout, "take timed out");
        }

        [Fact]
        public async Task WhileBufferIsFilledCountCanBeTaken()
        {
            this.testOutputHelper.WriteLine($"ProcessorCount={Environment.ProcessorCount}.");

            var fill = CreateParallelFill(buffer, threads: 4, itemsPerThread: 256);

            var count = Threaded.Run(4, () =>
            {
                int count = 0;

                while (!fill.IsCompleted)
                {
                    int newcount = buffer.Count;
                    newcount.Should().BeGreaterThanOrEqualTo(count);
                    count = newcount;
                }
            });

            await fill.TimeoutAfter(Timeout, "fill timed out");
            await count.TimeoutAfter(Timeout, "count timed out");
        }

        private Task CreateParallelFill(MpmcBoundedBuffer<string> buffer, int threads, int itemsPerThread)
        {
            return Threaded.Run(threads, () =>
            {
                var spin = new SpinWait();
                int count = 0;
                while (count < itemsPerThread)
                {
                    while (true)
                    {
                        if (buffer.TryAdd("hello") == BufferStatus.Success)
                        {
                            break;
                        }
                        spin.SpinOnce();
                    }
                    count++;
                }
            });
        }
    }
}
