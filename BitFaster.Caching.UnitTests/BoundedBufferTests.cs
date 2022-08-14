using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class BoundedBufferTests
    {
        private readonly BoundedBuffer<int> buffer = new BoundedBuffer<int>(10);

        [Fact]
        public void WhenSizeIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new BoundedBuffer<int>(-1); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SizeIsPowerOfTwo()
        {
            buffer.Capacity.Should().Be(16);
        }

        [Fact]
        public void WhenBufferIsEmptyCountIsZero()
        {
            buffer.Count.Should().Be(0);
        }

        [Fact]
        public void WhenBufferHasOneItemCountIsOne()
        {
            buffer.TryAdd(1);
            buffer.Count.Should().Be(1);
        }

        [Fact]
        public void WhenBufferIsFullTryAddIsFalse()
        {
            for (int i = 0; i < 16; i++)
            {
                buffer.TryAdd(i).Should().BeTrue();
            }

            buffer.TryAdd(666).Should().BeFalse();
        }

        [Fact]
        public void WhenBufferIsEmptyTryTakeIsFalse()
        {
            buffer.TryTake(out var _).Should().BeFalse();
        }

        [Fact]
        public void WhenItemAddedItCanBeTaken()
        {
            buffer.TryAdd(123).Should().BeTrue();
            buffer.TryTake(out var item).Should().BeTrue();
            item.Should().Be(123);
        }

        [Fact]
        public void WhenItemsAreAddedClearRemovesItems()
        {
            buffer.TryAdd(1);
            buffer.TryAdd(2);

            buffer.Count.Should().Be(2);

            buffer.Clear();

            buffer.Count.Should().Be(0);
            buffer.TryTake(out var _).Should().BeFalse();
        }
    }
}
