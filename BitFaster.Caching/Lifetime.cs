using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents the lifetime of a value. The value is alive and valid for use until the 
    /// lifetime is disposed.
    /// </summary>
    /// <typeparam name="T">The type of value</typeparam>
    public class Lifetime<T> : IDisposable
    {
        private readonly Action onDisposeAction;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the Lifetime class.
        /// </summary>
        /// <param name="value">The value to keep alive.</param>
        /// <param name="onDisposeAction">The action to perform when the lifetime is terminated.</param>
        public Lifetime(T value, Action onDisposeAction)
        {
            this.Value = value;
            this.onDisposeAction = onDisposeAction;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Terminates the lifetime and performs any cleanup required to release the value.
        /// </summary>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.onDisposeAction();
                this.isDisposed = true;
            }
        }
    }
}
