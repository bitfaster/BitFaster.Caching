using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class ThreadPoolSchedulerTests
    {
        private ThreadPoolScheduler scheduler = new ThreadPoolScheduler();

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
            var tcs = new TaskCompletionSource<bool>();
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { tcs.SetResult(true); });

            await tcs.Task;

            scheduler.LastException.HasValue.Should().BeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            var tcs = new TaskCompletionSource<bool>();
            scheduler.Run(() => { throw new InvalidCastException(); });
            scheduler.Run(() => { tcs.SetResult(true); });

            await tcs.Task;
            await scheduler.WaitForExceptionAsync();

            scheduler.LastException.HasValue.Should().BeTrue();
            scheduler.LastException.Value.Should().BeOfType<InvalidCastException>();
        }
    }
}
