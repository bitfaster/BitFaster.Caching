using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Initializes a new instance of the ReferenceCount class with the specified value.
        /// Initial count is 1.
        /// </summary>
        /// <param name="value"></param>
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
        public override bool Equals(object? obj)
        {
            return Equals(obj as ReferenceCount<TValue>);
        }

        ///<inheritdoc/>
        public bool Equals(ReferenceCount<TValue>? other)
        {
            return other != null &&
                   EqualityComparer<TValue>.Default.Equals(value, other.value) &&
                   count == other.count;
        }

        ///<inheritdoc/>
        public override int GetHashCode()
        {
            var hashCode = -1491496004;
            hashCode = hashCode * -1521134295 + EqualityComparer<TValue>.Default.GetHashCode(value!);
            hashCode = hashCode * -1521134295 + count.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Determines whether two ReferenceCount instances are the exact same value via a reference equality check.
        /// </summary>
        /// <param name="left">The left ReferenceCount to compare, or null.</param>
        /// <param name="right">The right ReferenceCount to compare, or null.</param>
        /// <returns>true if the value of left is the same as the value of right; otherwise, false.</returns>
        public static bool operator ==(ReferenceCount<TValue>? left, ReferenceCount<TValue>? right)
        {
            return object.ReferenceEquals(left, right);
        }

        /// <summary>
        /// Determines whether two ReferenceCount instances are different via a reference equality check.
        /// </summary>
        /// <param name="left">The left ReferenceCount to compare, or null.</param>
        /// <param name="right">The right ReferenceCount to compare, or null.</param>
        /// <returns>true if the value of left is different from the value of right; otherwise, false.</returns>
        public static bool operator !=(ReferenceCount<TValue>? left, ReferenceCount<TValue>? right)
        {
            return !(left == right);
        }
    }
}
