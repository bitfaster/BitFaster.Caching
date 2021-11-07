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

        /// <summary>
        /// Gets the value.
        /// </summary>
        public TValue Value
        {
            get
            {
                return this.value;
            }
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count
        {
            get
            {
                return this.count;
            }
        }

        /// <summary>
        /// Create a copy of the ReferenceCount with the count incremented by 1.
        /// </summary>
        /// <returns>A copy of the ReferenceCount with the count incremented by 1.</returns>
        public ReferenceCount<TValue> IncrementCopy()
        {
            return new ReferenceCount<TValue>(this.value, this.count + 1);
        }

        /// <summary>
        /// Create a copy of the ReferenceCount with the count decremented by 1.
        /// </summary>
        /// <returns>A copy of the ReferenceCount with the count decremented by 1.</returns>
        public ReferenceCount<TValue> DecrementCopy()
        {
            return new ReferenceCount<TValue>(this.value, this.count - 1);
        }

        ///<inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as ReferenceCount<TValue>);
        }

        ///<inheritdoc/>
        public bool Equals(ReferenceCount<TValue> other)
        {
            return other != null &&
                   EqualityComparer<TValue>.Default.Equals(value, other.value) &&
                   count == other.count;
        }

        ///<inheritdoc/>
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
