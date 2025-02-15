using System;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.UnitTests.Scheduler
{
    public static class SchedulerTestExtensions
    {
        public static async Task WaitForExceptionAsync(this IScheduler scheduler)
        {
            // Kind of a hack to wait for exceptions to propogate from background threads.
            int attempts = 0;
            while (!scheduler.LastException.HasValue)
            {
                await Task.Yield();

                if (attempts++ > 100)
                {
                    break;
                }
            }

            attempts = 80;
            while (!scheduler.LastException.HasValue)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1));

                if (attempts++ > 100)
                {
                    break;
                }
            }
        }
    }
}
