using Shouldly;
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
            expiryCalculator.TimeToExpire.ShouldBe(expiry.ToTimeSpan());
        }

        [Fact]
        public void AfterCreateReturnsTimeToExpire()
        { 
            expiryCalculator.GetExpireAfterCreate(1, 2).ShouldBe(expiry);
        }

        [Fact]
        public void AfteReadReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterRead(1, 2, Duration.SinceEpoch()).ShouldBe(expiry);
        }

        [Fact]
        public void AfteUpdateReturnsTimeToExpire()
        {
            expiryCalculator.GetExpireAfterUpdate(1, 2, Duration.SinceEpoch()).ShouldBe(expiry);
        }
    }
}
