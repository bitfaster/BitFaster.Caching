using System;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ExpireAfterAccessTests
    {
        private readonly Duration expiry = Duration.FromMinutes(1);
        private readonly ExpireAfterAccess<int, int> expiryCalculator;

        public ExpireAfterAccessTests()
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
        public void AfteReadReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterRead(1, 2, Duration.SinceEpoch()).Should().Be(expiry);
        }

        [Fact]
        public void AfteUpdateReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterUpdate(1, 2, Duration.SinceEpoch()).Should().Be(expiry);
        }
    }
}
