using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lazy
{
    public class AsyncAtomicTests
    {
        [Fact]
        public void WhenNotInitializedIsValueCreatedReturnsFalse()
        {
            AsyncAtomic<int, int> a = new();

            a.IsValueCreated.Should().Be(false);
        }

        [Fact]
        public void WhenNotInitializedValueIfCreatedReturnsDefault()
        {
            AsyncAtomic<int, int> a = new();

            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenInitializedByValueIsValueCreatedReturnsTrue()
        {
            AsyncAtomic<int, int> a = new(1);

            a.IsValueCreated.Should().Be(true);
        }

        [Fact]
        public void WhenInitializedByValueValueIfCreatedReturnsValue()
        {
            AsyncAtomic<int, int> a = new(1);

            a.ValueIfCreated.Should().Be(1);
        }

        [Fact]
        public async Task WhenNotInitGetValueReturnsValueFromFactory()
        {
            AsyncAtomic<int, int> a = new();

            int r = await a.GetValueAsync(1, k => Task.FromResult(k + 1));
            r.Should().Be(2);
        }

        [Fact]
        public async Task WhenInitGetValueReturnsInitialValue()
        {
            AsyncAtomic<int, int> a = new();

            int r1 = await a.GetValueAsync(1, k => Task.FromResult(k + 1));
            int r2 = await a.GetValueAsync(1, k => Task.FromResult(k + 12));
            r2.Should().Be(2);
        }
    }
}
