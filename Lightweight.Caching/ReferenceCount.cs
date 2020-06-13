using System;
using System.Collections.Generic;
using System.Text;

namespace Lightweight.Caching
{
	/// <summary>
	/// A reference counting class suitable for use with compare and swap algorithms.
	/// </summary>
	/// <typeparam name="TValue">The value type.</typeparam>
	public class ReferenceCount<TValue>
	{
		private readonly TValue value;
		private readonly int count;

		public ReferenceCount(TValue value)
		{
			this.value = value;
			this.count = 1;
		}

		private ReferenceCount(TValue value, int referenceCount)
		{
			this.value = value;
			this.count = referenceCount;
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
				return this.count;
			}
		}

		public override int GetHashCode()
		{
			return this.value.GetHashCode() ^ this.count;
		}

		public override bool Equals(object obj)
		{
			ReferenceCount<TValue> refCount = obj as ReferenceCount<TValue>;
			return refCount != null && refCount.Value != null && refCount.Value.Equals(this.value) && refCount.count == this.count;
		}

		public ReferenceCount<TValue> IncrementCopy()
		{
			if (this.count < 0)
			{
				throw new InvalidOperationException($"{typeof(TValue).Name} is no longer referenced.");
			}

			return new ReferenceCount<TValue>(this.value, this.count + 1);
		}

		public ReferenceCount<TValue> DecrementCopy()
		{
			return new ReferenceCount<TValue>(this.value, this.count - 1);
		}
	}
}
