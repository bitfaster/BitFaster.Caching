using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Lightweight.Caching.UnitTests
{
	public class ClassicLruTests
	{
		private const int capacity = 3;

		private ClassicLru<int, string> lru = new ClassicLru<int, string>(1, capacity, EqualityComparer<int>.Default);
		ValueFactory valueFactory = new ValueFactory();

		[Fact]
		public void WhenItemIsAddedCountIsCorrect()
		{
			lru.Count.Should().Be(0);
			lru.GetOrAdd(1, valueFactory.Create);
			lru.Count.Should().Be(1);
		}

		[Fact]
		public void WhenItemExistsTryGetReturnsValueAndTrue()
		{
			lru.GetOrAdd(1, valueFactory.Create);
			bool result = lru.TryGet(1, out var value);

			result.Should().Be(true);
			value.Should().Be("1");
		}

		[Fact]
		public void WhenItemDoesNotExistTryGetReturnsNullAndFalse()
		{
			lru.GetOrAdd(1, valueFactory.Create);
			bool result = lru.TryGet(2, out var value);

			result.Should().Be(false);
			value.Should().BeNull();
		}

		[Fact]
		public void WhenKeyIsRequestedItIsCreatedAndCached()
		{
			var result1 = lru.GetOrAdd(1, valueFactory.Create);
			var result2 = lru.GetOrAdd(1, valueFactory.Create);

			valueFactory.timesCalled.Should().Be(1);
			result1.Should().Be(result2);
		}

		[Fact]
		public void WhenDifferentKeysAreRequestedValueIsCreatedForEach()
		{
			var result1 = lru.GetOrAdd(1, valueFactory.Create);
			var result2 = lru.GetOrAdd(2, valueFactory.Create);

			valueFactory.timesCalled.Should().Be(2);

			result1.Should().Be("1");
			result2.Should().Be("2");
		}

		[Fact]
		public void WhenMoreKeysRequestedThanCapacityCountDoesNotIncrease()
		{
			for (int i = 0; i < capacity + 1; i++)
			{
				lru.GetOrAdd(i, valueFactory.Create);
			}

			lru.Count.Should().Be(capacity);
			valueFactory.timesCalled.Should().Be(capacity + 1);
		}

		[Fact]
		public void WhenMoreKeysRequestedThanCapacityOldestItemIsEvicted()
		{
			// request 10 items, LRU is now full
			for (int i = 0; i < capacity; i++)
			{
				lru.GetOrAdd(i, valueFactory.Create);
			}

			valueFactory.timesCalled.Should().Be(capacity);

			// request 0, now item 1 is to be evicted
			lru.GetOrAdd(0, valueFactory.Create);
			valueFactory.timesCalled.Should().Be(capacity);

			// request next item after last, verify value factory was called
			lru.GetOrAdd(capacity, valueFactory.Create);
			valueFactory.timesCalled.Should().Be(capacity + 1);

			// request 0, verify value factory not called
			lru.GetOrAdd(0, valueFactory.Create);
			valueFactory.timesCalled.Should().Be(capacity + 1);

			// request 1, verify value factory is called (and it was therefore not cached)
			lru.GetOrAdd(1, valueFactory.Create);
			valueFactory.timesCalled.Should().Be(capacity + 2);
		}

		[Fact]
		public void WhenKeyDoesNotExistTryGetReturnsFalse()
		{
			lru.GetOrAdd(1, valueFactory.Create);

			lru.TryGet(2, out var result).Should().Be(false);
		}

		[Fact]
		public void WhenKeyExistsTryGetReturnsTrueAndOutValueIsCorrect()
		{
			lru.GetOrAdd(1, valueFactory.Create);

			bool result = lru.TryGet(1, out var value);
			result.Should().Be(true);
			value.Should().Be("1");
		}

		private class ValueFactory
		{
			public int timesCalled;

			public string Create(int key)
			{
				timesCalled++;
				return key.ToString();
			}
		}
	}
}
