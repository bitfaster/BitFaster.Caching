using System;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ExpireAfterWriteTests
    {
        private readonly Duration expiry = Duration.FromMinutes(1);
        private readonly ExpireAfterWrite<int, int> expiryCalculator;

        public ExpireAfterWriteTests()
        {
            expiryCalculator = new(expiry.ToTimeSpan());
        }

        [Fact]
        public void TimeToExpireReturnsCtorArg()
        {
            expiryCalculator.TimeToExpire.Should().Be(expiry.ToTimeSpan());
        }

        [Fact]
        public void AfterCreateReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterCreate(1, 2).Should().Be(expiry);
        }

        [Fact]
        public void AfteReadReturnsCurrentTimeToExpire()
        {
            var current = new Duration(123);
            expiryCalculator.GetExpireAfterRead(1, 2, current).Should().Be(current);
        }

        [Fact]
        public void AfteUpdateReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterUpdate(1, 2, Duration.SinceEpoch()).Should().Be(expiry);
        }
    }
}
