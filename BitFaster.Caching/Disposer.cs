using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public static class Disposer<T>
    { 
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
