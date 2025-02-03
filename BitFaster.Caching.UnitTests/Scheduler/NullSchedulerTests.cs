using BitFaster.Caching.Scheduler;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public class NullSchedulerTests
    {
        private NullScheduler scheduler = new NullScheduler();

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
        public void WhenWorkIsScheduledItIsNotRun()
        {
            bool run = false;

            scheduler.Run(() => { run = true; });

            run.ShouldBeFalse();
        }

        [Fact]
        public void LastExceptionIsEmpty()
        {
            scheduler.LastException.HasValue.ShouldBeFalse();
        }
    }
}
