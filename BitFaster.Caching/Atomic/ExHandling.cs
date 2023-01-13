using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Atomic
{
    internal class ExHandling
    {
        internal static int EnumerateCount(IEnumerator enumerator)
        {
            int i = 0;
            while (enumerator.MoveNext())
            {
                i++;
            }
            return i;
        }
    }
}
