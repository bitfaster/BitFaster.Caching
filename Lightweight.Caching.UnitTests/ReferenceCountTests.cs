using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Lightweight.Caching.UnitTests
{
    public class ReferenceCountTests
    {
        [Fact]
        public void WhenOtherIsEqualEqualsReturnsTrue()
        {
            var a = new ReferenceCount<object>();
            var b = a.IncrementCopy().DecrementCopy();

            a.Should().Be(b);
        }

        [Fact]
        public void WhenOtherIsNotEqualEqualsReturnsFalse()
        {
            var a = new ReferenceCount<object>();
            var b = new ReferenceCount<object>();

            a.Should().NotBe(b);
        }

        [Fact]
        public void WhenOtherRefCountIsNotEqualEqualsReturnsFalse()
        {
            var a = new ReferenceCount<int>();
            var b = a.IncrementCopy();

            a.Should().NotBe(b);
        }

        [Fact]
        public void WhenObjectsAreEqualHashcodesAreEqual()
        {
            var a = new ReferenceCount<object>();
            var b = a.IncrementCopy().DecrementCopy();

            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void WhenObjectsAreDifferentHashcodesAreDifferent()
        {
            var a = new ReferenceCount<object>();
            var b = new ReferenceCount<object>();

            a.GetHashCode().Should().NotBe(b.GetHashCode());
        }
    }
}
