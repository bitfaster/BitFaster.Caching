using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ReferenceCountTests
    {
        [Fact]
        public void WhenOtherIsEqualEqualsReturnsTrue()
        {
            var a = new ReferenceCount<object>(new object());
            var b = a.IncrementCopy().DecrementCopy();

            a.ShouldBe(b);
        }

        [Fact]
        public void WhenOtherIsEqualReferenceEqualsReturnsFalse()
        {
            var a = new ReferenceCount<object>(new object());
            var b = a.IncrementCopy().DecrementCopy();

            a.ShouldNotBeSameAs(b);
        }

        [Fact]
        public void WhenOtherIsNotEqualEqualsReturnsFalse()
        {
            var a = new ReferenceCount<object>(new object());
            var b = new ReferenceCount<object>(new object());

            a.ShouldNotBe(b);
        }

        [Fact]
        public void WhenOtherRefCountIsNotEqualEqualsReturnsFalse()
        {
            var a = new ReferenceCount<int>(0);
            var b = a.IncrementCopy();

            a.ShouldNotBe(b);
        }

        [Fact]
        public void WhenObjectsAreEqualHashcodesAreEqual()
        {
            var a = new ReferenceCount<object>(new object());
            var b = a.IncrementCopy().DecrementCopy();

            a.GetHashCode().ShouldBe(b.GetHashCode());
        }

        [Fact]
        public void WhenObjectsAreDifferentHashcodesAreDifferent()
        {
            var a = new ReferenceCount<object>(new object());
            var b = new ReferenceCount<object>(new object());

            a.GetHashCode().ShouldNotBe(b.GetHashCode());
        }

        [Fact]
        public void WhenObjectIsNullGetHashCodeDoesntThrow()
        {
            var a = new ReferenceCount<object>(null);

            // nullable static analysis suggests this is broken, but it is legal to call
            // EqualityComparer<TValue>.Default.GetHashCode(null)
            a.GetHashCode();
        }
    }
}
