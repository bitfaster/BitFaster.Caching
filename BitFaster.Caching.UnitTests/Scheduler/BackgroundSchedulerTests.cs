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
    public class BackgroundSchedulerTests : IDisposable
    {
        private BackgroundThreadScheduler scheduler = new BackgroundThreadScheduler();

        [Fact]
        public void IsBackground()
        {
            scheduler.IsBackground.Should().BeTrue();
        }

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
            scheduler.RunCount.Should().Be(0);


            scheduler.Run(() => { });
            await Task.Yield();

            scheduler.LastException.HasValue.Should().BeFalse();
        }

        [Fact]
        public async Task WhenWorkThrowsLastExceptionIsPopulated()
        {
            TaskCompletionSource tcs = new TaskCompletionSource();
            scheduler.Run(() => { tcs.SetResult();  throw new InvalidCastException(); });

            await tcs.Task;

            // TODO: really bad
            while (!scheduler.LastException.HasValue)
            {
                await Task.Delay(1);
            }

            scheduler.LastException.HasValue.Should().BeTrue();
            scheduler.LastException.Value.Should().BeOfType<InvalidCastException>();
        }

        [Fact]
        public void WhenBacklogExceededThrows()
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            Action start = () => 
            {
                for (int i = 0; i < 17; i++)
                {
                    scheduler.Run(() => { tcs.Task.Wait(); });
                }
            };

            start.Should().Throw<InvalidOperationException>();
            tcs.SetResult();
        }

        [Fact]
        public async Task WhenDisposedRunsToCompletion()
        {
            this.scheduler.Dispose();

            var completion = scheduler.Completion;

            if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(1))) != completion)
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
