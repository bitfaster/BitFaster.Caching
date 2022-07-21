using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Synchronized
{
    public class AsyncAtomTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new AsyncAtom<int, int>();

            a.IsValueCreated.Should().BeFalse();
            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new AsyncAtom<int, int>(1);

            a.ValueIfCreated.Should().Be(1);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedValueReturned()
        {
            var a = new AsyncAtom<int, int>();
            (await a.GetValueAsync(1, k => Task.FromResult(2))).Should().Be(2);

            a.ValueIfCreated.Should().Be(2);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public async Task WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new AsyncAtom<int, int>();
            await a.GetValueAsync(1, k => Task.FromResult(2));
            (await a.GetValueAsync(1, k => Task.FromResult(3))).Should().Be(2);
        }

        [Fact]
        public async Task WhenValueCreateThrowsValueIsNotStored()
        {
            var a = new AsyncAtom<int, int>();

            Func<Task> getOrAdd = async () => { await a.GetValueAsync(1, k => throw new ArithmeticException()); };

            await getOrAdd.Should().ThrowAsync<ArithmeticException>();

            (await a.GetValueAsync(1, k => Task.FromResult(3))).Should().Be(3);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atom = new AsyncAtom<int, int>();
            var result = 0;
            var winners = 0;

            Task<int> first = atom.GetValueAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                result = 1;
                Interlocked.Increment(ref winners);
                return 1;
            });

            Task<int> second = atom.GetValueAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                result = 2;
                Interlocked.Increment(ref winners);
                return 2;
            });

            await enter.Task;
            resume.SetResult(true);

            (await first).Should().Be(result);
            (await second).Should().Be(result);

            winners.Should().Be(1);
        }
    }
}
