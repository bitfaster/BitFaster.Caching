using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class ScopedAsyncAtomicFactoryTests
    {
        [Fact]
        public void WhenScopeIsNotCreatedScopeIfCreatedReturnsNull()
        {
            var atomicFactory = new ScopedAsyncAtomicFactory<int, Disposable>();

            atomicFactory.ScopeIfCreated.ShouldBeNull();
        }

        [Fact]
        public void WhenScopeIsCreatedScopeIfCreatedReturnsScope()
        {
            var expectedDisposable = new Disposable();
            var atomicFactory = new ScopedAsyncAtomicFactory<int, Disposable>(expectedDisposable);

            atomicFactory.ScopeIfCreated.ShouldNotBeNull();
            atomicFactory.ScopeIfCreated.TryCreateLifetime(out var lifetime).ShouldBeTrue();
            lifetime.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public void WhenNotInitTryCreateReturnsFalse()
        {
            var sa = new ScopedAsyncAtomicFactory<int, Disposable>();
            sa.TryCreateLifetime(out var l).ShouldBeFalse();
        }

        [Fact]
        public void WhenCreatedTryCreateLifetimeReturnsScope()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAsyncAtomicFactory<int, Disposable>(expectedDisposable);

            sa.TryCreateLifetime(out var lifetime).ShouldBeTrue();
            lifetime.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public async Task WhenCreateFromValueLifetimeContainsValue()
        {
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>(new IntHolder() { actualNumber = 1 });

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, k =>
            {
                return Task.FromResult(new Scoped<IntHolder>(new IntHolder() { actualNumber = 2 }));
            });

            result.r.ShouldBeTrue();
            result.l.Value.actualNumber.ShouldBe(1);
        }

        [Fact]
        public async Task WhenCreateFromFactoryLifetimeContainsValue()
        {
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, k =>
            {
                return Task.FromResult(new Scoped<IntHolder>(new IntHolder() { actualNumber = 2 }));
            });

            result.r.ShouldBeTrue();
            result.l.Value.actualNumber.ShouldBe(2);
        }

        [Fact]
        public async Task WhenCreateFromFactoryArgLifetimeContainsValue()
        {
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var factory = CreateArgFactory(7);

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, factory);

            result.r.ShouldBeTrue();
            result.l.Value.actualNumber.ShouldBe(8);
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

            result.r.ShouldBeFalse();
            result.l.ShouldBeNull();
        }

        [Fact]
        public void WhenValueIsCreatedDisposeDisposesValue()
        {
            var holder = new IntHolder() { actualNumber = 2 };
            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>(holder);
            
            atomicFactory.Dispose();

            holder.disposed.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var winningNumber = 0;
            var winnerCount = 0;

            ValueTask<(bool r, Lifetime<IntHolder> l)> first = atomicFactory.TryCreateLifetimeAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                winningNumber = 1;
                Interlocked.Increment(ref winnerCount);
                return new Scoped<IntHolder>(new IntHolder() { actualNumber = 1 });
            });

            ValueTask<(bool r, Lifetime<IntHolder> l)> second = atomicFactory.TryCreateLifetimeAsync(1, async k =>
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

            result1.r.ShouldBeTrue();
            result2.r.ShouldBeTrue();

            result1.l.Value.actualNumber.ShouldBe(winningNumber);
            result2.l.Value.actualNumber.ShouldBe(winningNumber);
                
            winnerCount.ShouldBe(1);
        }

        [Fact]
        public async Task WhenDisposedWhileInitResultIsDisposed()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var holder = new IntHolder() { actualNumber = 1 };

            ValueTask<(bool r, Lifetime<IntHolder> l)> first = atomicFactory.TryCreateLifetimeAsync(1, async k =>
            {
                enter.SetResult(true);
                await resume.Task;

                return new Scoped<IntHolder>(holder);
            });

            await enter.Task;
            atomicFactory.Dispose();
            resume.SetResult(true);

            var result = await first;

            result.r.ShouldBeFalse();
            result.l.ShouldBeNull();

            holder.disposed.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenDisposedWhileThrowingNextInitIsDisposed()
        {
            var enter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var atomicFactory = new ScopedAsyncAtomicFactory<int, IntHolder>();
            var holder = new IntHolder() { actualNumber = 1 };

            ValueTask<(bool r, Lifetime<IntHolder> l)> first = atomicFactory.TryCreateLifetimeAsync(1, async k =>
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
            var ex = await tryCreateAsync.ShouldThrowAsync<InvalidOperationException>();;

            (bool r, Lifetime<IntHolder> l) result = await atomicFactory.TryCreateLifetimeAsync(1, k =>
            {
                return Task.FromResult(new Scoped<IntHolder>(holder));
            });

            result.r.ShouldBeFalse();
            result.l.ShouldBeNull();

            holder.disposed.ShouldBeTrue();
        }

        private static AsyncValueFactoryArg<int, int, Scoped<IntHolder>> CreateArgFactory(int arg)
        {
            return new AsyncValueFactoryArg<int, int, Scoped<IntHolder>>(
                (k, a) =>
                {
                    return Task.FromResult(new Scoped<IntHolder>(new IntHolder() { actualNumber = k + a }));
                },
                arg);
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
