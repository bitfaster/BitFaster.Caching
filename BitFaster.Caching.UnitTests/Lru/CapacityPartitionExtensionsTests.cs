using System;
using BitFaster.Caching.Lru;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class CapacityPartitionExtensionsTests
    {
        [Fact]
        public void WhenCapacityIsValidDoesNotThrow()
        {
            var p = new TestCapacityPartition { Cold = 2, Warm = 2, Hot = 2 };

            Action validate = () => { p.Validate(); };

            validate.ShouldNotThrow();
        }

        [Fact]
        public void WhenColdIsZeroThrows()
        {
            var p = new TestCapacityPartition { Cold = 0, Warm = 2, Hot = 2 };

            Action validate = () => { p.Validate(); };

            validate.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenWarmIsZeroThrows()
        {
            var p = new TestCapacityPartition { Cold = 2, Warm = 0, Hot = 2 };

            Action validate = () => { p.Validate(); };

            validate.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenHotIsZeroThrows()
        {
            var p = new TestCapacityPartition { Cold = 2, Warm = 2, Hot = 0 };

            Action validate = () => { p.Validate(); };

            validate.ShouldThrow<ArgumentOutOfRangeException>();
        }
    }
}
