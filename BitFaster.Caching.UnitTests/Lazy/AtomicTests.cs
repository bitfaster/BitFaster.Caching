using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lazy
{
    public class AtomicTests
    {
        [Fact]
        public void WhenNotInitializedIsValueCreatedReturnsFalse()
        {
            Atomic<int, int> a = new();

            a.IsValueCreated.Should().Be(false);
        }

        [Fact]
        public void WhenNotInitializedValueIfCreatedReturnsDefault()
        {
            Atomic<int, int> a = new();

            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenInitializedByValueIsValueCreatedReturnsTrue()
        {
            Atomic<int, int> a = new(1);

            a.IsValueCreated.Should().Be(true);
        }

        [Fact]
        public void WhenInitializedByValueValueIfCreatedReturnsValue()
        {
            Atomic<int, int> a = new(1);

            a.ValueIfCreated.Should().Be(1);
        }

        [Fact]
        public void WhenNotInitGetValueReturnsValueFromFactory()
        {
            Atomic<int, int> a = new();

            a.GetValue(1, k => k + 1).Should().Be(2);
        }

        [Fact]
        public void WhenInitGetValueReturnsInitialValue()
        {
            Atomic<int, int> a = new();

            a.GetValue(1, k => k + 1);
            a.GetValue(1, k => k + 2).Should().Be(2);
        }
    }
}
