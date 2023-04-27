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
    }
}
