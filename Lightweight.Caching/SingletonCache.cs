using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching
{
	public class SingletonCache<TKey, TValue>
			where TValue : new()
	{
		private readonly ConcurrentDictionary<TKey, ReferenceCount> cache = new ConcurrentDictionary<TKey, ReferenceCount>();

		public Handle Acquire(TKey key)
		{
			ReferenceCount refCount = this.cache.AddOrUpdate(key,
					(_) => new ReferenceCount(),
					(_, existingRefCount) => existingRefCount.IncrementCopy());

			return new Handle(key, refCount.Value, this);
		}

		private void Release(TKey key)
		{
			while (true)
			{
				ReferenceCount oldRefCount = this.cache[key];
				ReferenceCount newRefCount = oldRefCount.DecrementCopy();
				if (this.cache.TryUpdate(key, newRefCount, oldRefCount))
				{
					if (newRefCount.Count == 0)
					{
						// This will remove from dictionary only if key and the value with ReferenceCount (== 0) matches (under a lock)
						if (((IDictionary<TKey, ReferenceCount>)this.cache).Remove(new KeyValuePair<TKey, ReferenceCount>(key, newRefCount)))
						{
							if (newRefCount.Value is IDisposable d)
							{
								d.Dispose();
							}
						}
					}
					break;
				}
			}
		}

		private class ReferenceCount
		{
			private readonly TValue value;
			private readonly int referenceCount;

			public ReferenceCount()
			{
				this.value = new TValue();
				this.referenceCount = 1;
			}

			private ReferenceCount(TValue value, int referenceCount)
			{
				this.value = value;
				this.referenceCount = referenceCount;
			}

			public TValue Value
			{
				get
				{
					return this.value;
				}
			}

			public int Count
			{
				get
				{
					return this.referenceCount;
				}
			}

			public override int GetHashCode()
			{
				return this.value.GetHashCode() ^ this.referenceCount;
			}

			public override bool Equals(object obj)
			{
				ReferenceCount refCount = obj as ReferenceCount;
				return refCount != null && refCount.Value != null && refCount.Value.Equals(this.value) && refCount.referenceCount == this.referenceCount;
			}

			public ReferenceCount IncrementCopy()
			{
				return new ReferenceCount(this.value, this.referenceCount + 1);
			}

			public ReferenceCount DecrementCopy()
			{
				return new ReferenceCount(this.value, this.referenceCount - 1);
			}
		}

		public sealed class Handle : IDisposable
		{
			private TKey key;
			private TValue value;
			private SingletonCache<TKey, TValue> cache;

			public Handle(TKey key, TValue value, SingletonCache<TKey, TValue> cache)
			{
				this.key = key;
				this.value = value;
				this.cache = cache;
			}

			public TValue Value
			{
				get
				{
					return this.value;
				}
			}

			public void Dispose()
			{
				if (this.cache != null)
				{
					this.cache.Release(this.key);
					this.cache = null;
				}
			}
		}
	}
}
