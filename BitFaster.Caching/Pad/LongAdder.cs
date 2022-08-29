/*
 * Written by Doug Lea with assistance from members of JCP JSR-166
 * Expert Group and released to the public domain, as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 * 
 * See
 * http://hg.openjdk.java.net/jdk9/jdk9/jdk/file/65464a307408/src/java.base/share/classes/java/util/concurrent/atomic/LongAdder.java
 */

namespace BitFaster.Caching.Pad
{
    public sealed class LongAdder : Striped64
    {
        public LongAdder() { }

        public long Sum()
        {
            var @as = this.Cells; Cell a;
            var sum = @base.GetValue();
            if (@as != null)
            {
                for (var i = 0; i < @as.Length; ++i)
                {
                    if ((a = @as[i]) != null)
                        sum += a.value.GetValue();
                }
            }
            return sum;
        }

        public void Increment()
        {
            Add(1L);
        }

        public void Add(long value)
        {
            Cell[] @as;
            long b, v;
            int m;
            Cell a;
            if ((@as = this.Cells) != null || !@base.CompareAndSwap(b = @base.GetValue(), b + value))
            {
                var uncontended = true;
                if (@as == null || (m = @as.Length - 1) < 0 || (a = @as[GetProbe() & m]) == null || !(uncontended = a.value.CompareAndSwap(v = a.value.GetValue(), v + value)))
                {
                    LongAccumulate(value, uncontended);
                }
            }
        }
    }
}
