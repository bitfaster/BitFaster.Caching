using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class BitOpsTests
    {
        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]
        public void IntCeilingPowerOfTwo(int input, int power)
        {
            BitOps.CeilingPowerOfTwo(input).ShouldBe(power);
        }

        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]
        [InlineData(34359738368, 34359738368)]
        public void LongCeilingPowerOfTwo(long input, long power)
        {
            BitOps.CeilingPowerOfTwo(input).ShouldBe(power);
        }

        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]

        public void UIntCeilingPowerOfTwo(uint input, uint power)
        {
            BitOps.CeilingPowerOfTwo(input).ShouldBe(power);
        }

        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]
        [InlineData(34359738368, 34359738368)]

        public void UlongCeilingPowerOfTwo(ulong input, ulong power)
        {
            BitOps.CeilingPowerOfTwo(input).ShouldBe(power);
        }

        [Theory]
        [InlineData(0, 64)]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(1_000_000, 6)]
        [InlineData(34359738368, 35)]
        [InlineData(4611686018427387904, 62)]
        [InlineData(long.MaxValue, 0)]

        public void LongTrailingZeroCount(long input, int count)
        {
            BitOps.TrailingZeroCount(input).ShouldBe(count);
        }

        [Theory]
        [InlineData(0, 64)]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(1_000_000, 6)]
        [InlineData(34359738368, 35)]
        [InlineData(9223372036854775808, 63)]
        [InlineData(ulong.MaxValue, 0)]

        public void ULongTrailingZeroCount(ulong input, int count)
        {
            BitOps.TrailingZeroCount(input).ShouldBe(count);
        }

        [Fact]
        public void IntBitCount()
        {
            BitOps.BitCount(666).ShouldBe(5);
        }

        [Fact]
        public void LongtBitCount()
        {
            BitOps.BitCount(666L).ShouldBe(5);
        }

        [Fact]
        public void ULongtBitCount()
        {
            BitOps.BitCount(666UL).ShouldBe(5);
        }
    }
}
