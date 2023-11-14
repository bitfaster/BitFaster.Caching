
using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new AtomicFactory<int, int>();

            a.IsValueCreated.Should().BeFalse();
            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new AtomicFactory<int, int>(1);

            a.ValueIfCreated.Should().Be(1);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedValueReturned()
        {
            var a = new AtomicFactory<int, int>();
            a.GetValue(1, k => 2).Should().Be(2);

            a.ValueIfCreated.Should().Be(2);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedWithArgValueReturned()
        {
            var a = new AtomicFactory<int, int>();
            a.GetValue(1, (k, a) => k + a, 7).Should().Be(8);

            a.ValueIfCreated.Should().Be(8);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new AtomicFactory<int, int>();
            a.GetValue(1, k => 2);
            a.GetValue(1, k => 3).Should().Be(2);
        }

        [Fact]
        public void WhenValueCreatedArgGetValueReturnsOriginalValue()
        {
            var a = new AtomicFactory<int, int>();
            a.GetValue(1, (k, a) => k + a, 7);
            a.GetValue(1, (k, a) => k + a, 9).Should().Be(8);
        }

        [Fact]
        public void WhenValueNotCreatedHashCodeIsZero()
        {
            new AtomicFactory<int, int>()
                .GetHashCode()
                .Should().Be(0);
        }

        [Fact]
        public void WhenValueCreatedHashCodeIsValueHashCode()
        {
            new AtomicFactory<int, int>(1)
                .GetHashCode()
                .Should().Be(1);
        }

        [Fact]
        public void WhenValueNotCreatedEqualsFalse()
        {
            var a = new AtomicFactory<int, int>();
            var b = new AtomicFactory<int, int>();

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void WhenOtherValueNotCreatedEqualsFalse()
        {
            var a = new AtomicFactory<int, int>(1);
            var b = new AtomicFactory<int, int>();

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void WhenArgNullEqualsFalse()
        {
            new AtomicFactory<int, int>(1)
                .Equals(null)
                .Should().BeFalse();
        }

        [Fact]
        public void WhenArgObjectValuesAreSameEqualsTrue()
        {
            object other = new AtomicFactory<int, int>(1);

            new AtomicFactory<int, int>(1)
                .Equals(other)
                .Should().BeTrue();
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var atomicFactory = new AtomicFactory<int, int>();
            var result = 0;
            var winnerCount = 0;

            Task<int> first = Task.Run(() =>
            {
                return atomicFactory.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    result = 1;
                    Interlocked.Increment(ref winnerCount);
                    return 1;
                });
            });

            Task<int> second = Task.Run(() =>
            {
                return atomicFactory.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    result = 2;
                    Interlocked.Increment(ref winnerCount);
                    return 2;
                });
            });

            enter.WaitOne();
            resume.Set();

            (await first).Should().Be(result);
            (await second).Should().Be(result);

            winnerCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyAndFailExceptionIsPropogated()
        {
            var enter = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var atomicFactory = new AtomicFactory<int, int>();
            var throwCount = 0;

            Task<int> first = Task.Run(() =>
            {
                return atomicFactory.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    Interlocked.Increment(ref throwCount);
                    throw new Exception();
                });
            });

            Task<int> second = Task.Run(() =>
            {
                return atomicFactory.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    Interlocked.Increment(ref throwCount);
                    throw new Exception();
                });
            });

            enter.WaitOne();
            resume.Set();

            Func<Task> act1 = () => first;
            Func<Task> act2 = () => second;

            await act1.Should().ThrowAsync<Exception>();
            await act2.Should().ThrowAsync<Exception>();

            // verify only one exception was thrown
            throwCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyAndFailNewCallerStartsClean()
        {
            var enter = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var atomicFactory = new AtomicFactory<int, int>();

            Task<int> first = Task.Run(() =>
            {
                return atomicFactory.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();
                    throw new Exception();
                });
            });

            Task<int> second = Task.Run(() =>
            {
                return atomicFactory.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();
                    throw new Exception();
                });
            });

            enter.WaitOne();
            resume.Set();

            Func<Task> act1 = () => first;
            Func<Task> act2 = () => second;

            await act1.Should().ThrowAsync<Exception>();
            await act2.Should().ThrowAsync<Exception>();

            // verify exception is no longer cached
            atomicFactory.GetValue(1, k => k).Should().Be(1);
        }
    }
}
