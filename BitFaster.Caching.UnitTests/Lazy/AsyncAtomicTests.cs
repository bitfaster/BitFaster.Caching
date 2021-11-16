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

        [Fact]
        public async Task WhenGetValueThrowsExceptionIsNotCached()
        {
            AsyncAtomic<int, int> a = new();

            try
            {
                int r1 = await a.GetValueAsync(1, k => throw new Exception());

                throw new Exception("Expected GetValueAsync to throw");
            }
            catch
            {
            }
            
            int r2 = await a.GetValueAsync(1, k => Task.FromResult(k + 2));
            r2.Should().Be(3);
        }

        // TODO: this signal method is not reliable
        [Fact]
        public async Task WhenTaskIsCachedAllWaitersRecieveResult()
        {
            AsyncAtomic<int, int> a = new();

            TaskCompletionSource<int> valueFactory = new TaskCompletionSource<int>();
            TaskCompletionSource signal = new TaskCompletionSource();

            // Cache the task, don't wait
            var t1 = Task.Run(async () => await a.GetValueAsync(1, k => { signal.SetResult(); return valueFactory.Task; }));

            await signal.Task;

            var t2 = Task.Run(async () => await a.GetValueAsync(1, k => Task.FromResult(k + 2)));

            valueFactory.TrySetResult(666);

            int r2 = await t2;
            r2.Should().Be(666);
        }

        [Fact]
        public async Task WhenTaskIsCachedAndThrowsAllWaitersRecieveException()
        {
            AsyncAtomic<int, int> a = new();

            TaskCompletionSource<int> valueFactory = new TaskCompletionSource<int>();
            TaskCompletionSource signal = new TaskCompletionSource();

            // Cache the task, don't wait
            var t1 = Task.Run(async () => await a.GetValueAsync(1, k => { signal.SetResult(); return valueFactory.Task; }));

            await signal.Task;

            var t2 = Task.Run(async () => await a.GetValueAsync(1, k => Task.FromResult(k + 2)));

            valueFactory.SetException(new InvalidOperationException());

            Func<Task> r1 = async () => { await t1; };
            Func<Task> r2 = async () => { await t2; };

            r1.Should().Throw<InvalidOperationException>();
            r2.Should().Throw<InvalidOperationException>();
        }
    }
}
