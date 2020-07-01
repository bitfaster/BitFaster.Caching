using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public static class ThreadSafeRandom
    {
        [ThreadStatic]
        private static Random _local;

        private static Random _global = new Random();

        public static int Next(int max)
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global) seed = _global.Next();
                _local = inst = new Random(seed);
            }
            return inst.Next(max);
        }
    }
}
