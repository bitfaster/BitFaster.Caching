using System;
using BitFaster.Caching.Scheduler;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class ForegroundSchedulerTests
    {
        private ForegroundScheduler scheduler = new ForegroundScheduler();

        [Fact]
        public void IsNotBackground()
        {
            scheduler.IsBackground.ShouldBeFalse();
        }

        [Fact]
        public void WhenWorkIsScheduledCountIsIncremented()
        {
            scheduler.RunCount.ShouldBe(0);

            scheduler.Run(() => { });

            scheduler.RunCount.ShouldBe(1);
        }

        [Fact]
        public void WhenWorkIsScheduledItIsRun()
        {
            bool run = false;

            scheduler.Run(() => { run = true; });

            run.ShouldBeTrue();
        }

        [Fact]
        public void WhenWorkDoesNotThrowLastExceptionIsEmpty()
        {
            scheduler.RunCount.ShouldBe(0);

            scheduler.Run(() => { });

            scheduler.LastException.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void WhenWorkThrowsExceptionIsSynchronous()
        {
            Action work = () => { scheduler.Run(() => { throw new InvalidCastException(); }); };

            work.ShouldThrow<InvalidCastException>();
        }
    }
}
