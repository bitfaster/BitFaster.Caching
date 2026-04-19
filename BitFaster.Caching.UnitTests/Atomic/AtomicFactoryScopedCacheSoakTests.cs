﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    [Collection("Soak")]
    public class AtomicFactoryScopedCacheSoakTests
    {
        private const int capacity = 6;
        private const int threadCount = 4;
        private const int soakIterations = 10;
        private const int loopIterations = 100_000;

        [Theory]
        [Repeat(soakIterations)]
        public async Task ScopedGetOrAddLifetimeIsAlwaysAlive(int _)
        {
            var cache = new AtomicFactoryScopedCache<int, Disposable>(new ConcurrentLru<int, ScopedAtomicFactory<int, Disposable>>(1, capacity, EqualityComparer<int>.Default));

            var run = Threaded.Run(threadCount, _ =>
            {
                var key = new char[8];

                for (int i = 0; i < loopIterations; i++)
                {
                    using (var lifetime = cache.ScopedGetOrAdd(i, k => { return new Scoped<Disposable>(new Disposable(k)); }))
                    {
                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                    }
                }
            });

            await run;
        }
    }
}
