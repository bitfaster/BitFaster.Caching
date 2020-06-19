using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching
{
    /// <summary>
    /// A reference counting class suitable for use with compare and swap algorithms.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    public class ReferenceCount<TValue> : IEquatable<ReferenceCount<TValue>>
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

        public ReferenceCount<TValue> IncrementCopy()
        {
            if (this.count <= 0 && this.value is IDisposable)
            {
                throw new ObjectDisposedException($"{typeof(TValue).Name} is disposed.");
            }

            return new ReferenceCount<TValue>(this.value, this.count + 1);
        }

        public ReferenceCount<TValue> DecrementCopy()
        {
            return new ReferenceCount<TValue>(this.value, this.count - 1);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ReferenceCount<TValue>);
        }

        public bool Equals(ReferenceCount<TValue> other)
        {
            return other != null &&
                   EqualityComparer<TValue>.Default.Equals(value, other.value) &&
                   count == other.count;
        }

        public override int GetHashCode()
        {
            var hashCode = -1491496004;
            hashCode = hashCode * -1521134295 + EqualityComparer<TValue>.Default.GetHashCode(value);
            hashCode = hashCode * -1521134295 + count.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(ReferenceCount<TValue> left, ReferenceCount<TValue> right)
        {
            return EqualityComparer<ReferenceCount<TValue>>.Default.Equals(left, right);
        }

        public static bool operator !=(ReferenceCount<TValue> left, ReferenceCount<TValue> right)
        {
            return !(left == right);
        }
    }
}
