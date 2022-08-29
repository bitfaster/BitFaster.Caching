/*
 * Written by Doug Lea with assistance from members of JCP JSR-166
 * Expert Group and released to the public domain, as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 */

namespace BitFaster.Caching.Pad
{
    public sealed class StripedLongAdder : Striped64
    {
        public StripedLongAdder() { }

        public long GetValue()
        {
            var @as = this.Cells; Cell a;
            var sum = Base.GetValue();
            if (@as != null)
            {
                for (var i = 0; i < @as.Length; ++i)
                {
                    if ((a = @as[i]) != null)
                        sum += a.Value.GetValue();
                }
            }
            return sum;
        }

        public long NonVolatileGetValue()
        {
            var @as = this.Cells; Cell a;
            var sum = Base.NonVolatileGetValue();
            if (@as != null)
            {
                for (var i = 0; i < @as.Length; ++i)
                {
                    if ((a = @as[i]) != null)
                        sum += a.Value.NonVolatileGetValue();
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
            if ((@as = this.Cells) != null || !Base.CompareAndSwap(b = Base.GetValue(), b + value))
            {
                var uncontended = true;
                if (@as == null || (m = @as.Length - 1) < 0 || (a = @as[GetProbe() & m]) == null || !(uncontended = a.Value.CompareAndSwap(v = a.Value.GetValue(), v + value)))
                {
                    RetryUpdate(value, uncontended);
                }
            }
        }
    }
}
