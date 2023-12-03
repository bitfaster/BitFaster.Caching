using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class TimerWheelTests
    {
        [Fact]
        public void Test()
        {
            var wheel = new TimerWheel<int, int, TimeOrderNode<int, int>, ExpireAfterPolicy<int, int>>();
            var policy = new ExpireAfterPolicy<int, int>(wheel);
            var cache = new ConcurrentLfuCore<int, int, TimeOrderNode<int, int>, ExpireAfterPolicy<int, int>>(
                Defaults.ConcurrencyLevel, 3, new ThreadPoolScheduler(), EqualityComparer<int>.Default, () => { }, policy);
            
            wheel.Should().NotBeNull();
        }
    }
}
