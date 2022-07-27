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
    public readonly struct Optional<T> 
    {
        private readonly T value;
        private readonly bool hasValue;

        private Optional(T value)
        {
            this.value = value;
            this.hasValue = true;
        }

        public T Value => this.value;

        public bool HasValue => this.hasValue;

        public static Optional<T> From(T value)
        {
            return new Optional<T>(value);
        }

        public static Optional<T> None()
        {
            return new Optional<T>();
        }
    }
}
