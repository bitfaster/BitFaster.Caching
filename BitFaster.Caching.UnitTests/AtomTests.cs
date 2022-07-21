using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class AtomTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new Atom<int, int>();

            a.IsValueCreated.Should().BeFalse();
            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new Atom<int, int>(1);

            a.ValueIfCreated.Should().Be(1);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedValueReturned()
        {
            var a = new Atom<int, int>();
            a.GetValue(1, k => 2).Should().Be(2);

            a.ValueIfCreated.Should().Be(2);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new Atom<int, int>();
            a.GetValue(1, k => 2);
            a.GetValue(1, k => 3).Should().Be(2);
        }
    }
}
