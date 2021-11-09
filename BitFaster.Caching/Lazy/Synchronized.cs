using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    internal static class Synchronized
    {
        public static V Initialize<K, V>(ref V target, ref bool initialized, ref object syncLock, Func<K, V> valueFactory, K key)
        {
            // Fast path
            if (Volatile.Read(ref initialized))
            {
                return target;
            }

            lock (syncLock)
            {
                if (!Volatile.Read(ref initialized))
                {
                    target = valueFactory(key);
                    Volatile.Write(ref initialized, true);
                }
            }

            return target;
        }

        public static V Initialize<V>(ref V target, ref bool initialized, ref object syncLock, V value)
        {
            // Fast path
            if (Volatile.Read(ref initialized))
            {
                return target;
            }

            lock (syncLock)
            {
                if (!Volatile.Read(ref initialized))
                {
                    target = value;
                    Volatile.Write(ref initialized, true);
                }
            }

            return target;
        }
    }
}
