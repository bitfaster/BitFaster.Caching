using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class ThreadPoolSchedulerTests
    {
        private ThreadPoolScheduler scheduler = new ThreadPoolScheduler();

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
            var tcs = new TaskCompletionSource<bool>();
            scheduler.RunCount.ShouldBe(0);

            scheduler.Run(() => { tcs.SetResult(true); });

            await tcs.Task;

            scheduler.LastException.HasValue.ShouldBeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { throw new InvalidCastException(); });
            scheduler.Run(() => { tcs.SetResult(true); });

            await tcs.Task;
            await scheduler.WaitForExceptionAsync();

            scheduler.LastException.HasValue.ShouldBeTrue();
            scheduler.LastException.Value.ShouldBeOfType<InvalidCastException>();
        }
    }
}
