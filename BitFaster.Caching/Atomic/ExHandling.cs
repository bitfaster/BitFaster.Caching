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
            int count = 0;
            while (enumerator.MoveNext())
            {
                count++;
            }
            return count;
        }

        // TODO: how to filter keys?
    }
}
