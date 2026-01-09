
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// During reads, the policy evaluates ShouldDiscard and Touch. To avoid Getting the current time twice
    /// introduce a simple time class that holds the last time. This is class with a mutable field, because the 
    /// policy structs are readonly.
    /// </summary>
    /// <remarks>
    /// This class mitigates torn writes when running on 32-bit systems using Interlocked read and write.
    /// </remarks>
    internal class Time
    {
        private static readonly bool Is64Bit = Environment.Is64BitProcess;

        private long time;

        /// <summary>
        /// Gets or sets the last time.
        /// </summary>
        internal long Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Is64Bit)
                {
                    return time;
                }
                else
                {
                    return Interlocked.Read(ref time);
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (Is64Bit)
                {
                    time = value;
                }
                else
                {
                    Interlocked.CompareExchange(ref time, value, time);
                }
            }
        }
    }
}
