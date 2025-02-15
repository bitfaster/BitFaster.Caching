﻿using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class ConcurrentDictionaryExtensionTests
    {
        private ConcurrentDictionary<int, AtomicFactory<int, int>> dictionary = new();
        private ConcurrentDictionary<int, AsyncAtomicFactory<int, int>> dictionaryAsync = new();

        [Fact]
        public void WhenItemIsAddedItCanBeRetrieved()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryGetValue(1, out int value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public async Task WhenItemIsAddedAsyncItCanBeRetrieved()
        {
            await dictionaryAsync.GetOrAddAsync(1, k => Task.FromResult(k));

            dictionaryAsync.TryGetValue(1, out int value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public void WhenItemIsAddedWithArgItCanBeRetrieved()
        {
            dictionary.GetOrAdd(1, (k,a) => k + a, 2);

            dictionary.TryGetValue(1, out int value).ShouldBeTrue();
            value.ShouldBe(3);
        }

        [Fact]
        public async Task WhenItemIsAddedWithArgAsyncItCanBeRetrieved()
        {
            await dictionaryAsync.GetOrAddAsync(1, (k, a) => Task.FromResult(k + a), 2);

            dictionaryAsync.TryGetValue(1, out int value).ShouldBeTrue();
            value.ShouldBe(3);
        }

        [Fact]
        public void WhenKeyDoesNotExistTryGetReturnsFalse()
        {
            dictionary.TryGetValue(1, out int _).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistAsyncTryGetReturnsFalse()
        {
            dictionaryAsync.TryGetValue(1, out int _).ShouldBeFalse();
        }

        [Fact]
        public void WhenItemIsAddedItCanBeRemovedByKey()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryRemove(1, out int value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public async Task WhenItemIsAddedAsyncItCanBeRemovedByKey()
        {
            await dictionaryAsync.GetOrAddAsync(1, k => Task.FromResult(k));

            dictionaryAsync.TryRemove(1, out int value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public void WhenItemIsAddedItCanBeRemovedByKvp()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryRemove(new KeyValuePair<int, int>(1, 1)).ShouldBeTrue();
            dictionary.TryGetValue(1, out _).ShouldBeFalse();
        }

        [Fact]
        public async Task WhenItemIsAddedAsyncItCanBeRemovedByKvp()
        {
            await dictionaryAsync.GetOrAddAsync(1, k => Task.FromResult(k));

            dictionaryAsync.TryRemove(new KeyValuePair<int, int>(1, 1)).ShouldBeTrue();
            dictionaryAsync.TryGetValue(1, out _).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            dictionary.TryRemove(1, out int _).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistAsyncTryRemoveReturnsFalse()
        {
            dictionaryAsync.TryRemove(1, out int _).ShouldBeFalse();
        }
    }
}
