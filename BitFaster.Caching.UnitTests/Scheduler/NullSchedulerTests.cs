using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class NullSchedulerTests
    {
        private NullScheduler scheduler = new NullScheduler();

        //[Fact]
        //public async Task AwaitNextIsCompleted()
        //{
        //    var task = scheduler.Next;

        //    if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))) != task)
        //    {
        //        throw new Exception("waiting for Task to complete timed out");
        //    }
        //}

        [Fact]
        public void WhenWorkIsScheduledCountIsIncremented()
        {
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { });

            scheduler.RunCount.Should().Be(1);
        }

        [Fact]
        public void WhenWorkIsScheduledItIsNotRun()
        {
            bool run = false;

            scheduler.Run(() => { run = true; });

            run.Should().BeFalse();
        }

        [Fact]
        public void LastExceptionIsEmpty()
        {
            scheduler.LastException.HasValue.Should().BeFalse();
        }
    }
}
