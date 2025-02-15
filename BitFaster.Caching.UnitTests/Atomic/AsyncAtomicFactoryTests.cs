using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AsyncAtomicFactoryTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new AsyncAtomicFactory<int, int>();

            a.IsValueCreated.ShouldBeFalse();
            a.ValueIfCreated.ShouldBe(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new AsyncAtomicFactory<int, int>(1);

            a.ValueIfCreated.ShouldBe(1);
            a.IsValueCreated.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedValueReturned()
        {
            var a = new AsyncAtomicFactory<int, int>();
            (await a.GetValueAsync(1, k => Task.FromResult(2))).ShouldBe(2);

            a.ValueIfCreated.ShouldBe(2);
            a.IsValueCreated.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedWithArgValueReturned()
        {
            var a = new AsyncAtomicFactory<int, int>();
            (await a.GetValueAsync(1, (k, a) => Task.FromResult(k + a), 7)).ShouldBe(8);

            a.ValueIfCreated.ShouldBe(8);
            a.IsValueCreated.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new AsyncAtomicFactory<int, int>();
            await a.GetValueAsync(1, k => Task.FromResult(2));
            (await a.GetValueAsync(1, k => Task.FromResult(3))).ShouldBe(2);
        }

        [Fact]
        public async Task WhenValueCreatedArgGetValueReturnsOriginalValue()
        {
            var a = new AsyncAtomicFactory<int, int>();
            await a.GetValueAsync(1, (k, a) => Task.FromResult(k + a), 7);
            (await a.GetValueAsync(1, (k, a) => Task.FromResult(k + a), 9)).ShouldBe(8);
        }

        [Fact]
        public async Task WhenValueCreateThrowsValueIsNotStored()
        {
            var a = new AsyncAtomicFactory<int, int>();

            Func<Task> getOrAdd = async () => { await a.GetValueAsync(1, k => throw new ArithmeticException()); };

            var ex = await getOrAdd.ShouldThrowAsync<ArithmeticException>();;

            (await a.GetValueAsync(1, k => Task.FromResult(3))).ShouldBe(3);
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

            (await first).ShouldBe(result);
            (await second).ShouldBe(result);

            winnerCount.ShouldBe(1);
        }

        [Fact]
        public void WhenValueNotCreatedHashCodeIsZero()
        {
            new AsyncAtomicFactory<int, int>()
                .GetHashCode()
                .ShouldBe(0);
        }

        [Fact]
        public void WhenValueCreatedHashCodeIsValueHashCode()
        {
            new AsyncAtomicFactory<int, int>(1)
                .GetHashCode()
                .ShouldBe(1);
        }

        [Fact]
        public void WhenValueNotCreatedEqualsFalse()
        {
            var a = new AsyncAtomicFactory<int, int>();
            var b = new AsyncAtomicFactory<int, int>();

            a.Equals(b).ShouldBeFalse();
        }

        [Fact]
        public void WhenOtherValueNotCreatedEqualsFalse()
        {
            var a = new AsyncAtomicFactory<int, int>(1);
            var b = new AsyncAtomicFactory<int, int>();

            a.Equals(b).ShouldBeFalse();
        }

        [Fact]
        public void WhenArgNullEqualsFalse()
        {
            new AsyncAtomicFactory<int, int>(1)
                .Equals(null)
                .ShouldBeFalse();
        }

        [Fact]
        public void WhenArgObjectValuesAreSameEqualsTrue()
        {
            object other = new AsyncAtomicFactory<int, int>(1);

            new AsyncAtomicFactory<int, int>(1)
                .Equals(other)
                .ShouldBeTrue();
        }
    }
}
