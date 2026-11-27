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

        [Fact]
        public async Task CompletionBeforeDisposalRemainsIncompleteAfterWorkRuns()
        {
            var workRan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            scheduler.Run(() => workRan.SetResult(true));

            try
            {
                await WaitForCompletionAsync(workRan.Task);

                var completion = scheduler.Completion;
                var completed = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromMilliseconds(100)));

                completed.Should().NotBeSameAs(completion);
            }
            finally
            {
                scheduler.Dispose();
                await WaitForCompletionAsync(scheduler.Completion);
            }
        }

        [Fact]
        public async Task CompletionWhenDisposedDuringWorkWaitsForWorkToFinish()
        {
            var workStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseWork = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            scheduler.Run(() =>
            {
                workStarted.SetResult(true);
                releaseWork.Task.GetAwaiter().GetResult();
            });

            try
            {
                await WaitForCompletionAsync(workStarted.Task);

                scheduler.Dispose();
                var completion = scheduler.Completion;
                var completed = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromMilliseconds(100)));

                completed.Should().NotBeSameAs(completion);
            }
            finally
            {
                releaseWork.SetResult(true);
                scheduler.Dispose();
                await WaitForCompletionAsync(scheduler.Completion);
            }
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

        private static async Task WaitForCompletionAsync(Task task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().BeSameAs(task, "the operation should finish within the timeout");
            await task;
        }
    }
}
