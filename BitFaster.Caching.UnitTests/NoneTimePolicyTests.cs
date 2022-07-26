using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class NoneTimePolicyTests
    {
        [Fact]
        public void CanExpireIsFalse()
        {
            NoneTimePolicy.Instance.CanExpire.Should().BeFalse();
        }

        [Fact]
        public void TimeToLiveIsInfinite()
        {
            NoneTimePolicy.Instance.TimeToLive.Should().Be(NoneTimePolicy.Infinite);
        }

        [Fact]
        public void TrimExpiredIsNoOp()
        {
            Action trimExpired = () => NoneTimePolicy.Instance.TrimExpired();

            trimExpired.Should().NotThrow();
        }
    }
}
