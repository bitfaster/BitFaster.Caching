#if NET9_0_OR_GREATER
using System;
using System.Threading;
#endif

namespace BitFaster.Caching.Counters
{
    /// <summary>
    /// A thread-safe counter suitable for high throuhgput counting across many concurrent threads.
    /// </summary>
#if NET9_0_OR_GREATER

    public sealed class Counter : Striped64
    {
        private readonly PaddedLong[] Deltas = new PaddedLong[Environment.ProcessorCount];

        /// <summary>
        /// Increment by 1.
        /// </summary>
        public void Increment()
        {
            Add(1L);
        }

        /// <summary>
        /// Adds the specified value.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(long value)
        {
            ref PaddedLong delta = ref Deltas[(uint)Thread.GetCurrentProcessorId() % (uint)Deltas.Length];
            Interlocked.Add(ref delta.value, value);
        }

        /// <summary>
        /// Computes the current count.
        /// </summary>
        /// <returns>The current count.</returns>
        public long Count()
        {
            long delta = 0;
            foreach (ref PaddedLong i in Deltas.AsSpan())
            { 
                delta += Interlocked.Read(ref i.value);
            }
            return delta;
        }
    }

#else
    /*
     * Written by Doug Lea with assistance from members of JCP JSR-166
     * Expert Group and released to the public domain, as explained at
     * http://creativecommons.org/publicdomain/zero/1.0/
     */
    public sealed class Counter : Striped64
    {
        /// <summary>
        /// Creates a new Counter with an intial sum of zero.
        /// </summary>
        public Counter() { }

        /// <summary>
        /// Computes the current count.
        /// </summary>
        /// <returns>The current count.</returns>
        public long Count()
        {
            var @as = this.Cells; Cell a;
            var sum = @base.VolatileRead();
            if (@as != null)
            {
                for (var i = 0; i < @as.Length; ++i)
                {
                    if ((a = @as[i]) != null)
                        sum += a.value.VolatileRead();
                }
            }
            return sum;
        }

        /// <summary>
        /// Increment by 1.
        /// </summary>
        public void Increment()
        {
            Add(1L);
        }

        /// <summary>
        /// Adds the specified value.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(long value)
        {
            Cell[]? @as;
            long b, v;
            int m;
            Cell a;
            if ((@as = this.Cells) != null || !@base.CompareAndSwap(b = @base.VolatileRead(), b + value))
            {
                var uncontended = true;
                if (@as == null || (m = @as.Length - 1) < 0 || (a = @as[GetProbe() & m]) == null || !(uncontended = a.value.CompareAndSwap(v = a.value.VolatileRead(), v + value)))
                {
                    LongAccumulate(value, uncontended);
                }
            }
        }
    }
#endif
}
