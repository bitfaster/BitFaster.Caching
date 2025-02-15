using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class BackgroundSchedulerTests : IDisposable
    {
        private BackgroundThreadScheduler scheduler = new BackgroundThreadScheduler();

        [Fact]
        public void IsBackground()
        {
            scheduler.IsBackground.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenWorkIsScheduledCountIsIncremented()
        {
            scheduler.RunCount.ShouldBe(0);

            scheduler.Run(() => { });
            await Task.Yield();

            scheduler.RunCount.ShouldBe(1);
        }

        [Fact]
        public async Task WhenWorkIsScheduledItIsRun()
        {
            bool run = false;

            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { Volatile.Write(ref run, true); tcs.SetResult(true); });
            await tcs.Task;

            Volatile.Read(ref run).ShouldBeTrue();
        }

        [Fact]
        public async Task WhenWorkDoesNotThrowLastExceptionIsEmpty()
        {
            scheduler.RunCount.ShouldBe(0);


            scheduler.Run(() => { });
            await Task.Yield();

            scheduler.LastException.HasValue.ShouldBeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { tcs.SetResult(true);  throw new InvalidCastException(); });

            await tcs.Task;
            await scheduler.WaitForExceptionAsync();

            scheduler.LastException.HasValue.ShouldBeTrue();
            scheduler.LastException.Value.ShouldBeOfType<InvalidCastException>();
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

            scheduler.RunCount.ShouldBe(BackgroundThreadScheduler.MaxBacklog);
        }

        [Fact]
        public async Task WhenDisposedRunsToCompletion()
        {
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
