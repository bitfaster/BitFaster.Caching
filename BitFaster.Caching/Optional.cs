using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents an optional value.
    /// </summary>
    public class Optional<T> 
    {
        private readonly T value;
        private readonly bool hasValue;

        public Optional()
        {
        }

        public Optional(T value)
        {
            this.value = value;
            this.hasValue = true;
        }

        /// <summary>
        /// Gets the value of the current Optional<T> object if it has been assigned a valid underlying value.
        /// </summary>
        public T Value => this.value;

        /// <summary>
        /// Gets a value indicating whether the current Optional<T> object has a valid value of its underlying type.
        /// </summary>
        public bool HasValue => this.hasValue;

        /// <summary>
        /// Creates an empty Optional<T>.
        /// </summary>
        /// <returns>An empty Optional<T>.</returns>
        public static Optional<T> None()
        {
            return new Optional<T>();
        }
    }
}
