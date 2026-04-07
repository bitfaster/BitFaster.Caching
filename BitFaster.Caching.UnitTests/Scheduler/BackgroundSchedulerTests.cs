using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class BackgroundSchedulerTests : IDisposable
    {
        private BackgroundThreadScheduler scheduler = new BackgroundThreadScheduler();
        private readonly ITestOutputHelper output;
        public BackgroundSchedulerTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Fact]
        public void IsBackground()
        {
            scheduler.IsBackground.Should().BeTrue();
        }

        [Fact]
        public async Task WhenWorkIsScheduledCountIsIncremented()
        {
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { });
            await Task.Yield();

            scheduler.RunCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenWorkIsScheduledItIsRun()
        {
            bool run = false;

            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { Volatile.Write(ref run, true); tcs.SetResult(true); });
            await tcs.Task;

            Volatile.Read(ref run).Should().BeTrue();
        }

        [Fact]
        public async Task WhenWorkDoesNotThrowLastExceptionIsEmpty()
        {
            scheduler.RunCount.Should().Be(0);


            scheduler.Run(() => { });
            await Task.Yield();

            scheduler.LastException.HasValue.Should().BeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { tcs.SetResult(true); throw new InvalidCastException(); });

            await tcs.Task;
            await scheduler.WaitForExceptionAsync();

            scheduler.LastException.HasValue.Should().BeTrue();
            scheduler.LastException.Value.Should().BeOfType<InvalidCastException>();
        }

        [Fact]
        public void WhenBacklogExceededTasksAreDropped()
        {
            var mre = new ManualResetEvent(false);

            for (int i = 0; i < BackgroundThreadScheduler.MaxBacklog * 2; i++)
            {
                scheduler.Run(() => { mre.WaitOne(); });
            }

            mre.Set();

            scheduler.RunCount.Should().BeCloseTo(BackgroundThreadScheduler.MaxBacklog, 1);
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenDisposedRunsToCompletion(int _)
        {
            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { tcs.SetResult(true); });
            await tcs.Task;

            this.scheduler.Dispose();

            var completion = scheduler.Completion;

            if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(60))) != completion)
            {
                if (this.scheduler.LastException.HasValue)
                {
                    output.WriteLine(this.scheduler.LastException.ToString());
                }

                throw new Exception("Failed to stop");
            }
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenDisposedImmediatelyRunsToCompletion(int _)
        {
            this.scheduler.Dispose();

            var completion = scheduler.Completion;

            if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(60))) != completion)
            {
                if (this.scheduler.LastException.HasValue)
                { 
                    output.WriteLine(this.scheduler.LastException.ToString()); 
                }

                throw new Exception("Failed to stop");
            }
        }

        [Fact]
        public async Task WhenDoubleDisposedRunsToCompletion()
        {
            this.scheduler.Dispose();
            this.scheduler.Dispose();

            var completion = scheduler.Completion;

            if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(60))) != completion)
            {
                throw new Exception("Failed to stop");
            }
        }

        public void Dispose()
        {
            this?.scheduler.Dispose();
        }
    }
}
