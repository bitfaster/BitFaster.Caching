using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AsyncAtomicFactoryTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new AsyncAtomicFactory<int, int>();

            a.IsValueCreated.Should().BeFalse();
            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new AsyncAtomicFactory<int, int>(1);

            a.ValueIfCreated.Should().Be(1);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedValueReturned()
        {
            var a = new AsyncAtomicFactory<int, int>();
            (await a.GetValueAsync(1, k => Task.FromResult(2))).Should().Be(2);

            a.ValueIfCreated.Should().Be(2);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedWithArgValueReturned()
        {
            var a = new AsyncAtomicFactory<int, int>();
            (await a.GetValueAsync(1, (k, a) => Task.FromResult(k + a), 7)).Should().Be(8);

            a.ValueIfCreated.Should().Be(8);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new AsyncAtomicFactory<int, int>();
            await a.GetValueAsync(1, k => Task.FromResult(2));
            (await a.GetValueAsync(1, k => Task.FromResult(3))).Should().Be(2);
        }

        [Fact]
        public async Task WhenValueCreatedArgGetValueReturnsOriginalValue()
        {
            var a = new AsyncAtomicFactory<int, int>();
            await a.GetValueAsync(1, (k, a) => Task.FromResult(k + a), 7);
            (await a.GetValueAsync(1, (k, a) => Task.FromResult(k + a), 9)).Should().Be(8);
        }

        [Fact]
        public async Task WhenValueCreateThrowsValueIsNotStored()
        {
            var a = new AsyncAtomicFactory<int, int>();

            Func<Task> getOrAdd = async () => { await a.GetValueAsync(1, k => throw new ArithmeticException()); };

            await getOrAdd.Should().ThrowAsync<ArithmeticException>();

            (await a.GetValueAsync(1, k => Task.FromResult(3))).Should().Be(3);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new AsyncAtomicFactory<int, int>();
            var result = 0;
            var winnerCount = 0;

            var first = atomicFactory.GetValueAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                result = 1;
                Interlocked.Increment(ref winnerCount);
                return 1;
            });

            var second = atomicFactory.GetValueAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                result = 2;
                Interlocked.Increment(ref winnerCount);
                return 2;
            });

            await enter.Task;
            resume.SetResult(true);

            (await first).Should().Be(result);
            (await second).Should().Be(result);

            winnerCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyWithFailureSameExceptionIsPropagated()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new AsyncAtomicFactory<int, int>();

            var first = atomicFactory.GetValueAsync(1, async k =>
                {
                    enter.SetResult(true);
                    await resume.Task;

                    throw new ArithmeticException("1");
                }).AsTask();

            var second = atomicFactory.GetValueAsync(1, async k =>
                {
                    enter.SetResult(true);
                    await resume.Task;

                    throw new InvalidOperationException("2");
                }).AsTask();

            await enter.Task;
            resume.SetResult(true);

            // Both tasks will throw, but the first one to complete will propagate its exception
            // Both exceptions should be the same. If they are not, there will be an aggregate exception.
            try
            {
                await Task.WhenAll(first, second)
                    .TimeoutAfter(TimeSpan.FromSeconds(5), "Tasks did not complete within the expected time. Exceptions are not propagated between callers correctly.");
            }
            catch (ArithmeticException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        [Fact]
        public void WhenValueNotCreatedHashCodeIsZero()
        {
            new AsyncAtomicFactory<int, int>()
                .GetHashCode()
                .Should().Be(0);
        }

        [Fact]
        public void WhenValueCreatedHashCodeIsValueHashCode()
        {
            new AsyncAtomicFactory<int, int>(1)
                .GetHashCode()
                .Should().Be(1);
        }

        [Fact]
        public void WhenValueNotCreatedEqualsFalse()
        {
            var a = new AsyncAtomicFactory<int, int>();
            var b = new AsyncAtomicFactory<int, int>();

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void WhenOtherValueNotCreatedEqualsFalse()
        {
            var a = new AsyncAtomicFactory<int, int>(1);
            var b = new AsyncAtomicFactory<int, int>();

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void WhenArgNullEqualsFalse()
        {
            new AsyncAtomicFactory<int, int>(1)
                .Equals(null)
                .Should().BeFalse();
        }

        [Fact]
        public void WhenArgObjectValuesAreSameEqualsTrue()
        {
            object other = new AsyncAtomicFactory<int, int>(1);

            new AsyncAtomicFactory<int, int>(1)
                .Equals(other)
                .Should().BeTrue();
        }
    }
}
