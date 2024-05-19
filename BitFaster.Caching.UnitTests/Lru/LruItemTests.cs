using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LruItemTests
    {
        [Fact]
        public void EqualsWithSameReferenceReturnsTrue()
        { 
            var item = new LruItem<int, int>(1, 2);

            item.Equals(item).Should().BeTrue();
        }

        [Fact]
        public void EqualsObjectWithSameReferenceReturnsTrue()
        { 
            var item = new LruItem<int, int>(1, 2);

            item.Equals((object)item).Should().BeTrue();
        }

        [Fact]
        public void EqualsWithSameValuesReturnsFalse()
        { 
            var item1 = new LruItem<int, int>(1, 2);
            var item2 = new LruItem<int, int>(1, 2);

            // this is used for CAS algorithms, so this must be false
            item1.Equals(item2).Should().BeFalse();
        }

        [Fact]
        public void GetHashCodeWithDifferentObjectsIsDifferent()
        { 
            var item1 = new LruItem<int, int>(1, 2);
            var item2 = new LruItem<int, int>(2, 1);

            item1.GetHashCode().Should().NotBe(item2.GetHashCode());
        }

        [Fact]
        public void GetHashCodeHandlesNulls()
        { 
            var item1 = new LruItem<object, object>(null, null);
            item1.GetHashCode().Should().NotBe(0);
        }
    }
}
