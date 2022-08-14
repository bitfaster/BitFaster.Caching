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
    public class BackgroundSchedulerTests
    {
        private BackgroundScheduler scheduler = new BackgroundScheduler();

        [Fact]
        public async Task WhenWorkIsScheduledCanAwaitResult()
        {
            scheduler.Run(() => { });
            var task = scheduler.Next;

            if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))) != task)
            {
                throw new Exception("waiting for Task to complete timed out");
            }
        }

        [Fact]
        public async Task WhenWorkIsScheduledCountIsIncremented()
        {
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { });
            await scheduler.Next;

            scheduler.RunCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenWorkIsScheduledItIsRun()
        {
            bool run = false;

            scheduler.Run(() => { run = true; });
            await scheduler.Next;

            run.Should().BeTrue();
        }

        [Fact]
        public async Task WhenWorkDoesNotThrowLastExceptionIsEmpty()
        {
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { });
            await scheduler.Next;

            scheduler.LastException.HasValue.Should().BeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            scheduler.Run(() => { throw new InvalidCastException(); });
            await scheduler.Next;

            scheduler.LastException.HasValue.Should().BeTrue();
            scheduler.LastException.Value.Should().BeOfType<InvalidCastException>();
        }
    }
}
