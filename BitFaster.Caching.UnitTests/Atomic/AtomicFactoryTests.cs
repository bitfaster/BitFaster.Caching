
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
            var enter1 = new ManualResetEvent(false);
            var enter2 = new ManualResetEvent(false);
            var factory = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var atomicFactory = new AtomicFactory<int, int>();
            var result = 0;
            var winnerCount = 0;

            Task<int> first = Task.Run(() =>
            {
                enter1.Set();
                return atomicFactory.GetValue(1, k =>
                {
                    factory.Set();
                    resume.WaitOne();

                    result = 1;
                    Interlocked.Increment(ref winnerCount);
                    return 1;
                });
            });

            Task<int> second = Task.Run(() =>
            {
                enter2.Set();
                return atomicFactory.GetValue(1, k =>
                {
                    factory.Set();
                    resume.WaitOne();

                    result = 2;
                    Interlocked.Increment(ref winnerCount);
                    return 2;
                });
            });

            enter1.WaitOne();
            enter2.WaitOne();
            factory.WaitOne();
            resume.Set();

            (await first).Should().Be(result);
            (await second).Should().Be(result);

            winnerCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyAndFailExceptionIsPropogated()
        {
            int count = 0, timesContended = 0;

            // Overlapping the value factory calls is hard to achieve reliably.
            // Therefore, try many times and verify it was possible to provoke
            // at least once.
            while (count++ < 256)
            { 
                var enter1 = new ManualResetEvent(false);
                var enter2 = new ManualResetEvent(false);
                var factory = new ManualResetEvent(false);
                var resume = new ManualResetEvent(false);

                var atomicFactory = new AtomicFactory<int, int>();
                var throwCount = 0;

                Task<int> first = Task.Run(() =>
                {
                    enter1.Set();
                    return atomicFactory.GetValue(1, k =>
                    {
                        factory.Set();
                        resume.WaitOne();

                        Interlocked.Increment(ref throwCount);
                        throw new Exception();
                    });
                });

                Task<int> second = Task.Run(() =>
                {
                    enter2.Set();
                    return atomicFactory.GetValue(1, k =>
                    {
                        factory.Set();
                        resume.WaitOne();

                        Interlocked.Increment(ref throwCount);
                        throw new Exception();
                    });
                });

                enter1.WaitOne();
                enter2.WaitOne();
                factory.WaitOne();
                resume.Set();

                Func<Task> act1 = () => first;
                Func<Task> act2 = () => second;

                await act1.Should().ThrowAsync<Exception>();
                await act2.Should().ThrowAsync<Exception>();

                if (throwCount == 1)
                {
                    timesContended++;
                }
            }

            timesContended.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyAndFailNewCallerStartsClean()
        {
            var enter1 = new ManualResetEvent(false);
            var enter2 = new ManualResetEvent(false);
            var factory = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var atomicFactory = new AtomicFactory<int, int>();

            Task<int> first = Task.Run(() =>
            {
                enter1.Set();
                return atomicFactory.GetValue(1, k =>
                {
                    factory.Set();
                    resume.WaitOne();
                    throw new Exception();
                });
            });

            Task<int> second = Task.Run(() =>
            {
                enter2.Set();
                return atomicFactory.GetValue(1, k =>
                {
                    factory.Set();
                    resume.WaitOne();
                    throw new Exception();
                });
            });

            enter1.WaitOne();
            enter2.WaitOne();
            factory.WaitOne();
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
