using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    [Collection("Soak")]
    public class ConcurrentTLfuSoakTests
    {
        private const int soakIterations = 10;
        private const int threads = 4;
        private const int loopIterations = 100_000;

        private readonly ITestOutputHelper output;

        public ConcurrentTLfuSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task GetOrAddWithExpiry(int iteration)
        {
            var lfu = new ConcurrentTLfu<int, string>(20, new ExpireAfterWrite<int, string>(TimeSpan.FromMilliseconds(10)));

            await Threaded.RunAsync(threads, async () =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    await lfu.GetOrAddAsync(i + 1, i => Task.FromResult(i.ToString()));
                }
            });

            this.output.WriteLine($"iteration {iteration} keys={string.Join(" ", lfu.Keys)}");

            // TODO: integrity check, including TimerWheel
        }
    }
}
