using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public static class CapacityPartitionExtensions
    {
        public static void Validate(this ICapacityPartition capacity)
        {
            if (capacity.Cold < 1)
            { 
                throw new ArgumentOutOfRangeException(nameof(capacity.Cold));
            }

            if (capacity.Warm < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity.Warm));
            }

            if (capacity.Hot < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity.Hot));
            }
        }
    }
}
