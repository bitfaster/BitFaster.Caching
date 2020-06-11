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
			ReferenceCount lockObjectHolder = this.cache.AddOrUpdate(key,
					(_) => new ReferenceCount(),
					(_, lockObjectHolder2) => lockObjectHolder2.PlusOneReferenceCountCopy());

			return new Handle(key, lockObjectHolder.Value, this);
		}

		private void Release(TKey key)
		{
			while (true)
			{
				ReferenceCount oldLockObjectHolder = this.cache[key];
				ReferenceCount newLockObjectHolder = oldLockObjectHolder.MinusOneReferenceCountCopy();
				if (this.cache.TryUpdate(key, newLockObjectHolder, oldLockObjectHolder))
				{
					if (newLockObjectHolder.Count == 0)
					{
						// This will remove from dictionary only if key and the value with ReferenceCount (== 0) matches (under a lock)
						if (((IDictionary<TKey, ReferenceCount>)this.cache).Remove(new KeyValuePair<TKey, ReferenceCount>(key, newLockObjectHolder)))
						{
							if (newLockObjectHolder.Value is IDisposable d)
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

			public ReferenceCount PlusOneReferenceCountCopy()
			{
				return new ReferenceCount(this.value, this.referenceCount + 1);
			}

			public ReferenceCount MinusOneReferenceCountCopy()
			{
				return new ReferenceCount(this.value, this.referenceCount - 1);
			}
		}

		public sealed class Handle : IDisposable
		{
			private TKey key;
			private TValue value;
			private SingletonCache<TKey, TValue> lockObjectCache;

			public Handle(TKey key, TValue lockObject, SingletonCache<TKey, TValue> lockObjectCache)
			{
				this.key = key;
				this.value = lockObject;
				this.lockObjectCache = lockObjectCache;
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
				if (this.lockObjectCache != null)
				{
					this.lockObjectCache.Release(this.key);
					this.lockObjectCache = null;
				}
			}
		}
	}
}
