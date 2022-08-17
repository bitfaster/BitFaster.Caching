using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            TaskCompletionSource tcs = new TaskCompletionSource();
            scheduler.Run(() => { Volatile.Write(ref run, true); tcs.SetResult(); });

            await tcs.Task;

            Volatile.Read(ref run).Should().BeTrue();
        }

        [Fact]
        public async Task WhenWorkDoesNotThrowLastExceptionIsEmpty()
        {
            TaskCompletionSource tcs = new TaskCompletionSource();
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { tcs.SetResult(); });

            await tcs.Task;

            scheduler.LastException.HasValue.Should().BeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            TaskCompletionSource tcs = new TaskCompletionSource();
            scheduler.Run(() => { throw new InvalidCastException(); });
            scheduler.Run(() => { tcs.SetResult(); });

            await tcs.Task;

            // TODO: really bad
            while (!scheduler.LastException.HasValue)
            {
                await Task.Delay(1);
            }

            scheduler.LastException.HasValue.Should().BeTrue();
            scheduler.LastException.Value.Should().BeOfType<InvalidCastException>();
        }
    }
}
