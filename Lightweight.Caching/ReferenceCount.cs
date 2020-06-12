using System;
using System.Collections.Generic;
using System.Text;

namespace Lightweight.Caching
{
	/// <summary>
	/// A reference counting class suitable for use with compare and swap algorithms.
	/// </summary>
	/// <typeparam name="TValue">The value type.</typeparam>
	public class ReferenceCount<TValue> where TValue : new()
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
			ReferenceCount<TValue> refCount = obj as ReferenceCount<TValue>;
			return refCount != null && refCount.Value != null && refCount.Value.Equals(this.value) && refCount.referenceCount == this.referenceCount;
		}

		public ReferenceCount<TValue> IncrementCopy()
		{
			return new ReferenceCount<TValue>(this.value, this.referenceCount + 1);
		}

		public ReferenceCount<TValue> DecrementCopy()
		{
			return new ReferenceCount<TValue>(this.value, this.referenceCount - 1);
		}
	}
}
