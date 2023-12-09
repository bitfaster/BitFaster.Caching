using System.Diagnostics;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents an optional value.
    /// </summary>
    [DebuggerDisplay("{Value}")]
    public class Optional<T> 
    {
        private readonly T? value;
        private readonly bool hasValue;

        /// <summary>
        /// Initializes a new instance of the Optional class.
        /// </summary>
        public Optional()
        {
        }

        /// <summary>
        /// Initializes a new instance of the Optional class with the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public Optional(T value)
        {
            this.value = value;
            this.hasValue = true;
        }

        /// <summary>
        /// Gets the value of the current Optional object if it has been assigned a valid underlying value.
        /// </summary>
        public T? Value => this.value;

        /// <summary>
        /// Gets a value indicating whether the current Optional object has a valid value of its underlying type.
        /// </summary>
        public bool HasValue => this.hasValue;

        /// <summary>
        /// Creates an empty Optional.
        /// </summary>
        /// <returns>An empty Optional.</returns>
        public static Optional<T> None()
        {
            return new Optional<T>();
        }
    }
}
