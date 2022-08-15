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
    public class ForegroundSchedulerTests
    {
        private ForegroundScheduler scheduler = new ForegroundScheduler();

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
        public void WhenWorkIsScheduledItIsRun()
        {
            bool run = false;

            scheduler.Run(() => { run = true; });

            run.Should().BeTrue();
        }

        [Fact]
        public void WhenWorkDoesNotThrowLastExceptionIsEmpty()
        {
            scheduler.RunCount.Should().Be(0);

            scheduler.Run(() => { });

            scheduler.LastException.HasValue.Should().BeFalse();
        }

        [Fact]
        public void WhenWorkThrowsExceptionIsSynchronous()
        {
            Action work = () => { scheduler.Run(() => { throw new InvalidCastException(); }); };

            work.Should().Throw<InvalidCastException>();
        }
    }
}
