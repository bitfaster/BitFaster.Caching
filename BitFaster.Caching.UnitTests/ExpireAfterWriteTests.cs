using Shouldly;
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
            expiryCalculator.TimeToExpire.ShouldBe(expiry.ToTimeSpan());
        }

        [Fact]
        public void AfterCreateReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterCreate(1, 2).ShouldBe(expiry);
        }

        [Fact]
        public void AfteReadReturnsCurrentTimeToExpire()
        {
            var current = new Duration(123);
            expiryCalculator.GetExpireAfterRead(1, 2, current).ShouldBe(current);
        }

        [Fact]
        public void AfteUpdateReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterUpdate(1, 2, Duration.SinceEpoch()).ShouldBe(expiry);
        }
    }
}
