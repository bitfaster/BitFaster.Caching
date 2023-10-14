using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Buffers
{
    [Collection("Soak")]
    public class MpscBoundedBufferSoakTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        public MpscBoundedBufferSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task WhileBufferIsFilledItemsCanBeTaken()
        {
            this.testOutputHelper.WriteLine($"ProcessorCount={Environment.ProcessorCount}.");

            var buffer = new MpscBoundedBuffer<string>(1024);

            var fill = Threaded.Run(4, () =>
            {
                var spin = new SpinWait();
                int count = 0;
                while (count < 256)
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

            var take = Task.Run(() =>
            {
                int taken = 0;

                while (taken < 1024)
                {
                    var spin = new SpinWait();
                    if (buffer.TryTake(out var _) == BufferStatus.Success)
                    {
                        taken++;
                    }
                    spin.SpinOnce();
                }
            });

            await fill.TimeoutAfter(Timeout, "fill timed out");
            await take.TimeoutAfter(Timeout, "take timed out");
        }

        [Fact]
        public async Task WhileBufferIsFilledBufferCanBeDrained()
        {
            this.testOutputHelper.WriteLine($"ProcessorCount={Environment.ProcessorCount}.");

            var buffer = new MpscBoundedBuffer<string>(1024);

            var fill = Threaded.Run(4, () =>
            {
                var spin = new SpinWait();
                int count = 0;
                while (count < 256)
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

            var drain = Task.Run(() =>
            {
                int drained = 0;
                var drainBuffer = new ArraySegment<string>(new string[1024]);

                while (drained < 1024)
                {
                    drained += buffer.DrainTo(drainBuffer);
                }
            });

            await fill.TimeoutAfter(Timeout, "fill timed out");
            await drain.TimeoutAfter(Timeout, "drain timed out");
        }
    }
}
