using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class LifetimeTests
    {
        [Fact]
        public void WhenLifetimeIsDisposedThenOnDisposeActionIsCalled()
        {
            var calls = 0;
            var lifetime = new Lifetime<object>(new ReferenceCount<object>(new object()), () => calls++);

            lifetime.Dispose();

            calls.Should().Be(1);
        }

        [Fact]
        public void WhenLifetimeIsDisposedMultipleTimesThenOnDisposeActionIsCalledOnce()
        {
            var calls = 0;
            var lifetime = new Lifetime<object>(new ReferenceCount<object>(new object()), () => Interlocked.Increment(ref calls));

            Parallel.For(0, 100, _ => lifetime.Dispose());

            calls.Should().Be(1);
        }
    }
}
