﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public struct NoTelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        public double HitRatio => 0.0;

        public long Total => 0;

        public long Hits => 0;

        public long Misses => 0;

        public long Evicted => 0;

        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
        {
            // no-op, nothing is registered
            add { }
            remove { }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementMiss()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementHit()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEventSource(object source)
        {
        }
    }
}
