using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// A generic wrapper for object disposal. Enables JIT to inline/remove object disposal if statement reducing code size.
    /// </summary>
    /// <typeparam name="T">The type of object to dispose</typeparam>
    public static class Disposer<T>
    { 
        /// <summary>
        /// Dispose value if it implements the IDisposable interface.
        /// </summary>
        /// <param name="value">The value to dispose.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(T value)
        {
            if (value is IDisposable d)
            {
                d.Dispose();
            }
        }
    }
}
