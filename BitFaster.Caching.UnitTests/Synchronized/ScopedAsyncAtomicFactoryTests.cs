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
    public class ScopedAsyncAtomicFactoryTests
    {
        [Fact]
        public async Task WhenCreateFromValueLifetimeContainsValue()
        {
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>(new IntHolder() { actualNumber = 1 });

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, k =>
            {
                return Task.FromResult(new Scoped<IntHolder>(new IntHolder() { actualNumber = 2 }));
            });

            result.r.Should().BeTrue();
            result.l.Value.actualNumber.Should().Be(1);
        }

        [Fact]
        public async Task WhenScopeIsDisposedTryCreateReturnsFalse()
        {
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>(new IntHolder() { actualNumber = 1 });
            atomicFactory.Dispose();

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, k =>
            {
                return Task.FromResult(new Scoped<IntHolder>(new IntHolder() { actualNumber = 2 }));
            });

            result.r.Should().BeFalse();
            result.l.Should().BeNull();
        }

        [Fact]
        public void WhenValueIsCreatedDisposeDisposesValue()
        {
            var holder = new IntHolder() { actualNumber = 2 };
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>(holder);
            
            atomicFactory.Dispose();

            holder.disposed.Should().BeTrue();
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var winningNumber = 0;
            var winnerCount = 0;

            Task<(bool r, Lifetime<IntHolder> l)> first = atomicFactory.TryCreateLifetimeAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                winningNumber = 1;
                Interlocked.Increment(ref winnerCount);
                return new Scoped<IntHolder>(new IntHolder() { actualNumber = 1 });
            });

            Task<(bool r, Lifetime<IntHolder> l)> second = atomicFactory.TryCreateLifetimeAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                winningNumber = 2;
                Interlocked.Increment(ref winnerCount);
                return new Scoped<IntHolder>(new IntHolder() { actualNumber = 2 });
            });

            await enter.Task;
            resume.SetResult(true);

            var result1 = await first;
            var result2 = await second;

            result1.r.Should().BeTrue();
            result2.r.Should().BeTrue();

            result1.l.Value.actualNumber.Should().Be(winningNumber);
            result2.l.Value.actualNumber.Should().Be(winningNumber);
                
            winnerCount.Should().Be(1);
        }

        [Fact]
        public async Task WhenDisposedWhileInitResultIsDisposed()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var holder = new IntHolder() { actualNumber = 1 };

            Task<(bool r, Lifetime<IntHolder> l)> first = atomicFactory.TryCreateLifetimeAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                return new Scoped<IntHolder>(holder);
            });

            await enter.Task;
            atomicFactory.Dispose();
            resume.SetResult(true);

            var result = await first;

            result.r.Should().BeFalse();
            result.l.Should().BeNull();

            holder.disposed.Should().BeTrue();
        }

        [Fact]
        public async Task WhenDisposedWhileThrowingNextInitIsDisposed()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var holder = new IntHolder() { actualNumber = 1 };

            Task<(bool r, Lifetime<IntHolder> l)> first = atomicFactory.TryCreateLifetimeAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                throw new InvalidOperationException();
            });

            await enter.Task;
            atomicFactory.Dispose();
            resume.SetResult(true);

            // At this point, the scoped value is not created but the initializer is marked
            // to dispose the item. If no further calls are made, there is nothing to dispose.
            // If we create an item, to be in a consistent state we should dispose it.

            Func<Task> tryCreateAsync = async () => { await first; };
            await tryCreateAsync.Should().ThrowAsync<InvalidOperationException>();

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, k =>
            {
                return Task.FromResult(new Scoped<IntHolder>(holder));
            });

            result.r.Should().BeFalse();
            result.l.Should().BeNull();

            holder.disposed.Should().BeTrue();
        }

        private class IntHolder : IDisposable
        {
            public bool disposed;
            public int actualNumber;

            public void Dispose()
            {
                disposed = true;
            }
        }
    }
}
