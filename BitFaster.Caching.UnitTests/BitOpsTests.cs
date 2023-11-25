using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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
            BitOps.CeilingPowerOfTwo(input).Should().Be(power);
        }

        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]
        [InlineData(34359738368, 34359738368)]
        public void LongCeilingPowerOfTwo(long input, long power)
        {
            BitOps.CeilingPowerOfTwo(input).Should().Be(power);
        }

        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]

        public void UIntCeilingPowerOfTwo(uint input, uint power)
        {
            BitOps.CeilingPowerOfTwo(input).Should().Be(power);
        }

        [Theory]
        [InlineData(3, 4)]
        [InlineData(7, 8)]
        [InlineData(15, 16)]
        [InlineData(536870913, 1073741824)]
        [InlineData(34359738368, 34359738368)]

        public void UlongCeilingPowerOfTwo(ulong input, ulong power)
        {
            BitOps.CeilingPowerOfTwo(input).Should().Be(power);
        }

        [Theory]
        [InlineData(0, 64)]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(1_000_000, 6)]
        [InlineData(34359738368, 35)]
        [InlineData(9223372036854775808, 63)]
        [InlineData(ulong.MaxValue, 0)]

        public void LongTrailingZeroCount(ulong input, int count)
        {
            BitOps.TrailingZeroCount(input).Should().Be(count);
        }

        [Fact]
        public void IntBitCount()
        {
            BitOps.BitCount(666).Should().Be(5);
        }

        [Fact]
        public void LongtBitCount()
        {
            BitOps.BitCount(666L).Should().Be(5);
        }

        [Fact]
        public void ULongtBitCount()
        {
            BitOps.BitCount(666UL).Should().Be(5);
        }
    }
}
